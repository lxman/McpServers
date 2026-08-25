using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Services;

/// <summary>
/// Orchestrates repository indexing: file discovery, chunking, embedding, and storage.
/// </summary>
public sealed class RepositoryIndexer(
    OllamaService ollamaService,
    QdrantService qdrantService,
    ChunkerFactory chunkerFactory,
    IndexStateStore indexStateStore,
    CollectionWriteCoordinator writeCoordinator,
    IOptions<CodeAssistOptions> options,
    ILogger<RepositoryIndexer> logger)
{
    private readonly CodeAssistOptions _options = options.Value;

    private const int EmbeddingBatchSize = 50;

    /// <summary>
    /// Index a repository, detecting changes since last index.
    /// </summary>
    public async Task<IndexingResult> IndexRepositoryAsync(
        string repositoryPath,
        string? repositoryName = null,
        IReadOnlyList<string>? includePatterns = null,
        IReadOnlyList<string>? excludePatterns = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var failedFiles = new List<string>();

        repositoryPath = Path.GetFullPath(repositoryPath);
        repositoryName ??= Path.GetFileName(repositoryPath);
        string collectionName = SanitizeCollectionName(repositoryName);

        await using IAsyncDisposable writeLease =
            await writeCoordinator.AcquireAsync(collectionName, cancellationToken);

        includePatterns ??= _options.DefaultIncludePatterns;
        excludePatterns ??= _options.DefaultExcludePatterns;

        logger.LogInformation("Starting indexing of repository {Repository} at {Path}",
            repositoryName, repositoryPath);

        try
        {
            // Ensure embedding model is available
            await ollamaService.EnsureModelAvailableAsync(cancellationToken);

            logger.LogDebug("Loading index state...");
            // Load existing index state
            IndexStateFile? existingState = await indexStateStore.LoadAsync(repositoryName, cancellationToken);
            ValidateRepositoryRoot(existingState, repositoryPath);
            Dictionary<string, IndexedFile> existingFiles = existingState?.Files ?? new Dictionary<string, IndexedFile>();

            string embeddingModel =
                await ollamaService.ResolveServerModelAsync(cancellationToken) ?? _options.EmbeddingModel;
            float[] dimensionProbe = await ollamaService.GetEmbeddingAsync("codeassist dimension probe", cancellationToken);
            ValidateEmbeddingCompatibility(existingState, embeddingModel, dimensionProbe.Length, _options.VectorDimension);

            logger.LogDebug("Ensuring collection exists...");
            // Ensure collection exists only after compatibility checks. A failed check must not mutate
            // an existing index or create a collection that cannot accept the server's vectors.
            await qdrantService.EnsureCollectionAsync(collectionName, cancellationToken);

            // One list, one place. Indexes were previously created here AND in EnsurePayloadIndexesAsync
            // with different field sets, so which fields were indexed depended on which path ran.
            await qdrantService.EnsurePayloadIndexesAsync(collectionName, cancellationToken);

            logger.LogDebug("Discovering files...");
            // Discover files to index
            List<string> filesToProcess = DiscoverFiles(repositoryPath, includePatterns, excludePatterns);
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Found {Count} files to process", filesToProcess.Count);

            // Categorize files
            (List<string> filesToAdd, List<string> filesToUpdate, List<string> filesToRemove, List<string> filesToSkip) =
                CategorizeFiles(filesToProcess, existingFiles, repositoryPath, cancellationToken);

            logger.LogInformation(
                "Files to add: {Add}, update: {Update}, remove: {Remove}, skip: {Skip}",
                filesToAdd.Count, filesToUpdate.Count, filesToRemove.Count, filesToSkip.Count);

            // Remove stale files from index
            foreach (string file in filesToRemove)
            {
                await qdrantService.DeleteByFilePathAsync(collectionName, file, cancellationToken);
            }

            // Process new and updated files in parallel
            var allChunks = new ConcurrentBag<CodeChunk>();
            var newFileStates = new ConcurrentDictionary<string, IndexedFile>();
            var replacedChunkIds = new ConcurrentDictionary<string, IReadOnlyList<Guid>>();
            var failedFilesBag = new ConcurrentBag<string>();

            List<string> filesToChunk = filesToAdd.Concat(filesToUpdate).ToList();
            var processedCount = 0;

            logger.LogInformation("Processing {Count} files in parallel...", filesToChunk.Count);

            var chunkSw = Stopwatch.StartNew();
            var activeFiles = new ConcurrentDictionary<string, DateTime>();

            await Parallel.ForEachAsync(
                filesToChunk,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cancellationToken
                },
                async (relativePath, ct) =>
                {
                    activeFiles[relativePath] = DateTime.UtcNow;
                    logger.LogDebug("Processing file: {File}", relativePath);
                    try
                    {
                        string fullPath = Path.Combine(repositoryPath, relativePath);
                        logger.LogDebug("Reading file: {File}", relativePath);
                        string content = await File.ReadAllTextAsync(fullPath, ct);
                        logger.LogDebug("Read {Bytes} bytes from {File}", content.Length, relativePath);
                        var fileInfo = new FileInfo(fullPath);

                        // Chunk the file
                        logger.LogDebug("Chunking file: {File}", relativePath);
                        string language = ChunkerFactory.GetLanguage(fullPath);
                        ICodeChunker chunker = chunkerFactory.GetChunker(fullPath);
                        IReadOnlyList<CodeChunk> chunks = chunker.ChunkCode(content, fullPath, relativePath, language);
                        logger.LogDebug("Created {Count} chunks for {File}", chunks.Count, relativePath);

                        // Snapshot exactly what existed before this replacement. Deleting by file path
                        // before chunking made a transient read, chunking, or embedding failure erase the
                        // last usable version. Deleting by file path after the upsert would erase the new
                        // points too, so retire only this snapshot once every replacement is stored.
                        List<Guid> oldIds = (await qdrantService.SearchByFilePathAsync(
                                collectionName, relativePath, cancellationToken: ct))
                            .Select(result => result.Chunk.Id)
                            .Distinct()
                            .ToList();
                        replacedChunkIds[relativePath] = oldIds;

                        foreach (CodeChunk chunk in chunks)
                        {
                            allChunks.Add(chunk);
                        }

                        // A successfully processed file belongs in the manifest even when its
                        // chunker returns no searchable content (for example an empty or
                        // declaration-free file). Omitting it makes every later refresh classify it
                        // as newly added again. For an updated file, the empty state also records
                        // that its previous points were deliberately retired.
                        newFileStates[relativePath] = CreateIndexedFileState(
                            relativePath, content, fileInfo.LastWriteTimeUtc, chunks);

                        activeFiles.TryRemove(relativePath, out _);
                        int count = Interlocked.Increment(ref processedCount);
                        if (count % 10 == 0 || count == filesToChunk.Count)
                        {
                            List<string> stuckFiles = activeFiles
                                .Where(kvp => (DateTime.UtcNow - kvp.Value).TotalSeconds > 2)
                                .Select(kvp => kvp.Key)
                                .Take(5)
                                .ToList();
                            string stuckInfo = stuckFiles.Count > 0
                                ? $" [SLOW: {string.Join(", ", stuckFiles.Select(f => Path.GetFileName(f)))}]"
                                : "";
                            logger.LogInformation("Chunked {Count}/{Total} ({Chunks} chunks, {Rate:F0}/sec){Stuck}",
                                count, filesToChunk.Count, allChunks.Count, count / chunkSw.Elapsed.TotalSeconds, stuckInfo);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        activeFiles.TryRemove(relativePath, out _);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        activeFiles.TryRemove(relativePath, out _);
                        logger.LogWarning(ex, "Failed to process file {FilePath}", relativePath);
                        failedFilesBag.Add(relativePath);
                    }
                });

            // Transfer failed files from concurrent bag to list
            failedFiles.AddRange(failedFilesBag);

            logger.LogInformation("Chunking complete: {FileCount} files, {ChunkCount} chunks",
                filesToChunk.Count - failedFiles.Count, allChunks.Count);

            // Embed and store chunks in batches
            List<CodeChunk> chunkList = allChunks.ToList();
            var totalChunks = 0;
            int totalBatches = (chunkList.Count + EmbeddingBatchSize - 1) / EmbeddingBatchSize;

            logger.LogInformation("Embedding {ChunkCount} chunks in {BatchCount} batches...",
                chunkList.Count, totalBatches);

            await StoreBeforeRetiringAsync(
                async ct =>
                {
                    var embedSw = Stopwatch.StartNew();
                    for (var i = 0; i < chunkList.Count; i += EmbeddingBatchSize)
                    {
                        ct.ThrowIfCancellationRequested();

                        List<CodeChunk> batch = chunkList.Skip(i).Take(EmbeddingBatchSize).ToList();
                        List<string> texts = batch.Select(c => c.Content).ToList();

                        int batchNum = i / EmbeddingBatchSize + 1;
                        if (batchNum % 5 == 0 || batchNum == totalBatches)
                        {
                            int embeddedSoFar = i + batch.Count;
                            double rate = embeddedSoFar / embedSw.Elapsed.TotalSeconds;
                            logger.LogInformation(
                                "Embedding batch {BatchNum}/{TotalBatches} ({Rate:F0} chunks/sec)",
                                batchNum, totalBatches, rate);
                        }

                        float[][] embeddings = await ollamaService.GetEmbeddingsAsync(texts, ct);
                        await qdrantService.UpsertChunksAsync(collectionName, batch, embeddings, ct);

                        totalChunks += batch.Count;
                    }
                },
                replacedChunkIds.Values,
                (oldIds, ct) => qdrantService.DeletePointsAsync(collectionName, oldIds, ct),
                cancellationToken);

            // Preserve unchanged file states
            foreach (string relativePath in filesToSkip)
            {
                if (!existingFiles.TryGetValue(relativePath, out IndexedFile? existingFile)) continue;
                newFileStates[relativePath] = existingFile;
                totalChunks += existingFile.ChunkCount;
            }


            // A failed update keeps both its old Qdrant points and its state entry. Omitting the state
            // entry would misreport the index and turn the next retry into an apparent add.
            foreach (string relativePath in failedFiles)
            {
                if (!existingFiles.TryGetValue(relativePath, out IndexedFile? existingFile)) continue;
                newFileStates[relativePath] = existingFile;
                totalChunks += existingFile.ChunkCount;
            }

            // Save updated index state
            string? gitCommit = GetGitCommitSha(repositoryPath);

            DateTimeOffset indexedAt = DateTimeOffset.UtcNow;
            var newState = new IndexStateFile
            {
                RepositoryName = repositoryName,
                RootPath = repositoryPath,
                LastCommitSha = failedFiles.Count == 0 ? gitCommit : existingState?.LastCommitSha,
                CreatedAt = existingState?.CreatedAt ?? DateTimeOffset.UtcNow,
                LastUpdatedAt = indexedAt,
                LastFullIndexAt = ResolveLastFullIndexAt(existingState, indexedAt, failedFiles.Count),
                LastPromotionAt = existingState?.LastPromotionAt,
                LastIndexFailedFiles = failedFiles.ToList(),
                EmbeddingModel = embeddingModel,
                VectorDimension = dimensionProbe.Length,
                CollectionName = collectionName,
                IncludePatterns = includePatterns.ToList(),
                ExcludePatterns = excludePatterns.ToList(),
                Files = newFileStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            await indexStateStore.SaveAsync(repositoryName, newState, cancellationToken);

            sw.Stop();

            var result = new IndexingResult
            {
                Success = true,
                FilesProcessed = filesToAdd.Count + filesToUpdate.Count,
                FilesAdded = filesToAdd.Count,
                FilesUpdated = filesToUpdate.Count,
                FilesRemoved = filesToRemove.Count,
                FilesSkipped = filesToSkip.Count,
                TotalChunks = totalChunks,
                Duration = sw.Elapsed,
                FailedFiles = failedFiles
            };

            logger.LogInformation(
                "Indexing complete in {Duration}. Added: {Added}, Updated: {Updated}, Removed: {Removed}, Chunks: {Chunks}",
                sw.Elapsed, filesToAdd.Count, filesToUpdate.Count, filesToRemove.Count, totalChunks);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Indexing cancelled for repository {Repository}", repositoryName);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Indexing failed for repository {Repository}", repositoryName);

            return new IndexingResult
            {
                Success = false,
                FilesProcessed = 0,
                FilesAdded = 0,
                FilesUpdated = 0,
                FilesRemoved = 0,
                FilesSkipped = 0,
                TotalChunks = 0,
                Duration = sw.Elapsed,
                ErrorMessage = ex.Message,
                FailedFiles = failedFiles
            };
        }
    }

    /// <summary>
    /// Search the indexed repository.
    /// </summary>
    public async Task<SearchResponse> SearchAsync(
        string repositoryName,
        string query,
        int limit = 10,
        float? minScore = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string collectionName = SanitizeCollectionName(repositoryName);

        float[] queryEmbedding = await ollamaService.GetEmbeddingAsync(query, cancellationToken);
        List<SearchResult> results = await qdrantService.SearchAsync(
            collectionName,
            queryEmbedding,
            limit,
            minScore ?? _options.MinSimilarityScore,
            cancellationToken: cancellationToken);

        sw.Stop();

        return new SearchResponse
        {
            Query = query,
            Results = results,
            Duration = sw.Elapsed,
            RepositoryName = repositoryName
        };
    }

    /// <summary>
    /// Get the current state of a repository index.
    /// </summary>
    public async Task<IndexState?> GetIndexStateAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        IndexStateFile? stateFile = await indexStateStore.LoadAsync(repositoryName, cancellationToken);
        if (stateFile == null) return null;

        return new IndexState
        {
            RepositoryName = stateFile.RepositoryName,
            RootPath = stateFile.RootPath,
            LastCommitSha = stateFile.LastCommitSha,
            CreatedAt = stateFile.CreatedAt,
            LastUpdatedAt = stateFile.LastUpdatedAt,
            LastFullIndexAt = stateFile.LastFullIndexAt,
            LastPromotionAt = stateFile.LastPromotionAt,
            LastIndexFailedFiles = stateFile.LastIndexFailedFiles,
            FileCount = stateFile.Files.Count,
            ChunkCount = stateFile.Files.Values.Sum(f => f.ChunkCount),
            EmbeddingModel = stateFile.EmbeddingModel,
            VectorDimension = stateFile.VectorDimension,
            CollectionName = stateFile.CollectionName,
            IncludePatterns = stateFile.IncludePatterns,
            ExcludePatterns = stateFile.ExcludePatterns
        };
    }

    /// <summary>
    /// List all indexed repositories.
    /// </summary>
    public async Task<List<string?>> ListIndexedRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        return (await indexStateStore.ListRepositoryNamesAsync(cancellationToken))
            .Select(name => (string?)name)
            .ToList();
    }

    /// <summary>
    /// Delete a repository index.
    /// </summary>
    public async Task DeleteIndexAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        // Loading first validates that a differently punctuated name did not collide with this state
        // file. The collection must not be deleted until its identity is known.
        await indexStateStore.LoadAsync(repositoryName, cancellationToken);

        string collectionName = SanitizeCollectionName(repositoryName);
        await qdrantService.DeleteCollectionAsync(collectionName, cancellationToken);

        indexStateStore.Delete(repositoryName);

        logger.LogInformation("Deleted index for repository {Repository}", repositoryName);
    }

    #region Helpers

    internal static void ValidateEmbeddingCompatibility(
        IndexStateFile? existingState,
        string currentModel,
        int actualDimension,
        int configuredDimension)
    {
        if (actualDimension != configuredDimension)
        {
            throw new InvalidOperationException(
                $"Embedding service returned {actualDimension}-dimension vectors, but VectorDimension "
                + $"is configured as {configuredDimension}. Correct the configuration before indexing.");
        }

        if (existingState is null) return;

        if (!string.Equals(existingState.EmbeddingModel, currentModel, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Index '{existingState.RepositoryName}' contains vectors from embedding model "
                + $"'{existingState.EmbeddingModel}', but the server now reports '{currentModel}'. "
                + "Delete and rebuild the index before using the new model.");
        }

        if (existingState.VectorDimension is int indexedDimension && indexedDimension != actualDimension)
        {
            throw new InvalidOperationException(
                $"Index '{existingState.RepositoryName}' contains {indexedDimension}-dimension vectors, "
                + $"but the embedding service now returns {actualDimension}. Delete and rebuild the index.");
        }
    }

    internal static DateTimeOffset? ResolveLastFullIndexAt(
        IndexStateFile? existingState,
        DateTimeOffset indexedAt,
        int failedFileCount)
    {
        return failedFileCount == 0 ? indexedAt : existingState?.LastFullIndexAt;
    }

    internal static IndexedFile CreateIndexedFileState(
        string relativePath,
        string content,
        DateTime lastModified,
        IReadOnlyList<CodeChunk> chunks)
    {
        return new IndexedFile
        {
            RelativePath = relativePath,
            ContentHash = ComputeFileHash(content),
            LastModified = lastModified,
            IndexedAt = DateTimeOffset.UtcNow,
            ChunkCount = chunks.Count,
            ChunkIds = chunks.Select(chunk => chunk.Id).ToList()
        };
    }

    internal static async Task StoreBeforeRetiringAsync(
        Func<CancellationToken, Task> storeReplacements,
        IEnumerable<IReadOnlyList<Guid>> replacedPointIds,
        Func<IReadOnlyList<Guid>, CancellationToken, Task> retirePoints,
        CancellationToken cancellationToken = default)
    {
        await storeReplacements(cancellationToken);

        // If storage throws, this loop is never reached and the previous searchable version remains.
        foreach (IReadOnlyList<Guid> pointIds in replacedPointIds)
        {
            await retirePoints(pointIds, cancellationToken);
        }
    }

    internal static void ValidateRepositoryRoot(IndexStateFile? existingState, string repositoryPath)
    {
        if (existingState is null) return;

        string existingRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(existingState.RootPath));
        string requestedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(existingRoot, requestedRoot, comparison)) return;

        throw new InvalidOperationException(
            $"Repository name '{existingState.RepositoryName}' is already assigned to '{existingRoot}' "
            + $"and cannot also index '{requestedRoot}'. Choose a distinct repository name.");
    }

    internal static List<string> DiscoverFiles(
        string repositoryPath,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<string> excludePatterns)
    {
        var matcher = new Matcher();

        foreach (string pattern in includePatterns)
        {
            matcher.AddInclude(NormalizeGlob(pattern));
        }

        foreach (string pattern in excludePatterns)
        {
            matcher.AddExclude(NormalizeGlob(pattern));
        }

        PatternMatchingResult result = matcher.Execute(new DirectoryInfoWrapper(
            new DirectoryInfo(repositoryPath)));

        // Normalize here rather than at the consumers: this value becomes the relative_path payload
        // key, and it must be byte-identical to what HotCache produces for the same file.
        return result.Files.Select(f => IndexPath.Normalize(f.Path)).ToList();
    }

    /// <summary>
    /// Normalize a glob pattern so simple extensions like "*.cs" match recursively.
    /// Microsoft.Extensions.FileSystemGlobbing treats "*.cs" as root-only;
    /// most users expect it to mean "all .cs files everywhere".
    /// </summary>
    private static string NormalizeGlob(string pattern)
    {
        // Already has a directory component — leave it alone
        if (pattern.Contains('/') || pattern.Contains('\\') || pattern.StartsWith("**/"))
            return pattern;

        // Simple wildcard like "*.cs" or "*.py" → make recursive
        return $"**/{pattern}";
    }

    private static (List<string> toAdd, List<string> toUpdate, List<string> toRemove, List<string> toSkip)
        CategorizeFiles(
            List<string> currentFiles,
            Dictionary<string, IndexedFile> existingFiles,
            string repositoryPath,
            CancellationToken cancellationToken = default)
    {
        var toAdd = new List<string>();
        var toUpdate = new List<string>();
        var toSkip = new List<string>();
        HashSet<string> currentFileSet = currentFiles.ToHashSet();

        foreach (string relativePath in currentFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.Combine(repositoryPath, relativePath);

            if (!existingFiles.TryGetValue(relativePath, out IndexedFile? existingFile))
            {
                toAdd.Add(relativePath);
                continue;
            }

            // Check if file has changed
            string currentHash = ComputeFileHash(File.ReadAllText(fullPath));
            if (currentHash != existingFile.ContentHash)
            {
                toUpdate.Add(relativePath);
            }
            else
            {
                toSkip.Add(relativePath);
            }
        }

        // Files that exist in index but not on disk
        List<string> toRemove = existingFiles.Keys
            .Where(f =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return !currentFileSet.Contains(f);
            })
            .ToList();

        return (toAdd, toUpdate, toRemove, toSkip);
    }

    private static string ComputeFileHash(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Delegates to the shared helper so this and L2PromotionService's fallback cannot drift apart —
    // see CollectionNaming for what happened when they did.
    private static string SanitizeCollectionName(string name) => CollectionNaming.ForRepository(name);

    private static string? GetGitCommitSha(string repositoryPath)
    {
        try
        {
            string gitDir = Path.Combine(repositoryPath, ".git");
            if (!Directory.Exists(gitDir)) return null;

            string headPath = Path.Combine(gitDir, "HEAD");
            if (!File.Exists(headPath)) return null;

            string headContent = File.ReadAllText(headPath).Trim();

            // HEAD might be a direct SHA or a ref
            if (!headContent.StartsWith("ref:"))
            {
                return headContent;
            }

            string refPath = headContent["ref:".Length..].Trim();
            string refFile = Path.Combine(gitDir, refPath);

            if (File.Exists(refFile))
            {
                return File.ReadAllText(refFile).Trim();
            }

            // Check packed-refs
            string packedRefsPath = Path.Combine(gitDir, "packed-refs");
            if (!File.Exists(packedRefsPath)) return null;
            string[] lines = File.ReadAllLines(packedRefsPath);
            return (from line in lines where line.EndsWith(refPath) select line.Split(' ')[0]).FirstOrDefault();

        }
        catch
        {
            return null;
        }
    }

    #endregion
}
