using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
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

        includePatterns ??= _options.DefaultIncludePatterns;
        excludePatterns ??= _options.DefaultExcludePatterns;

        logger.LogInformation("Starting indexing of repository {Repository} at {Path}",
            repositoryName, repositoryPath);

        try
        {
            // Ensure embedding model is available
            await ollamaService.EnsureModelAvailableAsync(cancellationToken);

            logger.LogDebug("Ensuring collection exists...");
            // Ensure collection exists
            await qdrantService.EnsureCollectionAsync(collectionName, cancellationToken);

            logger.LogDebug("Loading index state...");
            // Load existing index state
            IndexStateFile? existingState = await LoadIndexStateAsync(repositoryName, cancellationToken);
            Dictionary<string, IndexedFile> existingFiles = existingState?.Files ?? new Dictionary<string, IndexedFile>();

            logger.LogDebug("Discovering files...");
            // Discover files to index
            List<string> filesToProcess = DiscoverFiles(repositoryPath, includePatterns, excludePatterns);

            logger.LogInformation("Found {Count} files to process", filesToProcess.Count);

            // Categorize files
            (List<string> filesToAdd, List<string> filesToUpdate, List<string> filesToRemove, List<string> filesToSkip) =
                CategorizeFiles(filesToProcess, existingFiles, repositoryPath);

            logger.LogInformation(
                "Files to add: {Add}, update: {Update}, remove: {Remove}, skip: {Skip}",
                filesToAdd.Count, filesToUpdate.Count, filesToRemove.Count, filesToSkip.Count);

            // Remove stale files from index
            foreach (string file in filesToRemove)
            {
                await qdrantService.DeleteByFilePathAsync(collectionName, file, cancellationToken);
            }

            // Process new and updated files in parallel
            var allChunks = new System.Collections.Concurrent.ConcurrentBag<CodeChunk>();
            var newFileStates = new System.Collections.Concurrent.ConcurrentDictionary<string, IndexedFile>();
            var failedFilesBag = new System.Collections.Concurrent.ConcurrentBag<string>();

            List<string> filesToChunk = filesToAdd.Concat(filesToUpdate).ToList();
            var processedCount = 0;
            HashSet<string> updateHashSet = filesToUpdate.ToHashSet();

            logger.LogInformation("Processing {Count} files in parallel...", filesToChunk.Count);

            var chunkSw = Stopwatch.StartNew();
            var activeFiles = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>();

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

                        // Delete existing chunks if updating (must be sequential for Qdrant)
                        if (updateHashSet.Contains(relativePath))
                        {
                            await qdrantService.DeleteByFilePathAsync(collectionName, relativePath, ct);
                        }

                        // Chunk the file
                        logger.LogDebug("Chunking file: {File}", relativePath);
                        string language = ChunkerFactory.GetLanguage(fullPath);
                        ICodeChunker chunker = chunkerFactory.GetChunker(fullPath);
                        IReadOnlyList<CodeChunk> chunks = chunker.ChunkCode(content, fullPath, relativePath, language);
                        logger.LogDebug("Created {Count} chunks for {File}", chunks.Count, relativePath);

                        if (chunks.Count > 0)
                        {
                            foreach (CodeChunk chunk in chunks)
                            {
                                allChunks.Add(chunk);
                            }

                            newFileStates[relativePath] = new IndexedFile
                            {
                                RelativePath = relativePath,
                                ContentHash = ComputeFileHash(content),
                                LastModified = fileInfo.LastWriteTimeUtc,
                                IndexedAt = DateTimeOffset.UtcNow,
                                ChunkCount = chunks.Count,
                                ChunkIds = chunks.Select(c => c.Id).ToList()
                            };
                        }

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

            var embedSw = Stopwatch.StartNew();
            for (var i = 0; i < chunkList.Count; i += EmbeddingBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<CodeChunk> batch = chunkList.Skip(i).Take(EmbeddingBatchSize).ToList();
                List<string> texts = batch.Select(c => c.Content).ToList();

                int batchNum = i / EmbeddingBatchSize + 1;
                if (batchNum % 5 == 0 || batchNum == totalBatches)
                {
                    int embeddedSoFar = i + batch.Count;
                    double rate = embeddedSoFar / embedSw.Elapsed.TotalSeconds;
                    logger.LogInformation("Embedding batch {BatchNum}/{TotalBatches} ({Rate:F0} chunks/sec)",
                        batchNum, totalBatches, rate);
                }

                float[][] embeddings = await ollamaService.GetEmbeddingsAsync(texts, cancellationToken);
                await qdrantService.UpsertChunksAsync(collectionName, batch, embeddings, cancellationToken);

                totalChunks += batch.Count;
            }

            // Preserve unchanged file states
            foreach (string relativePath in filesToSkip)
            {
                if (!existingFiles.TryGetValue(relativePath, out IndexedFile? existingFile)) continue;
                newFileStates[relativePath] = existingFile;
                totalChunks += existingFile.ChunkCount;
            }

            // Save updated index state
            string? gitCommit = GetGitCommitSha(repositoryPath);
            var newState = new IndexStateFile
            {
                RepositoryName = repositoryName,
                RootPath = repositoryPath,
                LastCommitSha = gitCommit,
                CreatedAt = existingState?.CreatedAt ?? DateTimeOffset.UtcNow,
                LastUpdatedAt = DateTimeOffset.UtcNow,
                EmbeddingModel = _options.EmbeddingModel,
                CollectionName = collectionName,
                IncludePatterns = includePatterns.ToList(),
                ExcludePatterns = excludePatterns.ToList(),
                Files = newFileStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            await SaveIndexStateAsync(repositoryName, newState, cancellationToken);

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
        IndexStateFile? stateFile = await LoadIndexStateAsync(repositoryName, cancellationToken);
        if (stateFile == null) return null;

        return new IndexState
        {
            RepositoryName = stateFile.RepositoryName,
            RootPath = stateFile.RootPath,
            LastCommitSha = stateFile.LastCommitSha,
            CreatedAt = stateFile.CreatedAt,
            LastUpdatedAt = stateFile.LastUpdatedAt,
            FileCount = stateFile.Files.Count,
            ChunkCount = stateFile.Files.Values.Sum(f => f.ChunkCount),
            EmbeddingModel = stateFile.EmbeddingModel,
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
        string stateDir = _options.IndexStateDirectory;
        if (!Directory.Exists(stateDir))
        {
            return [];
        }

        string[] files = Directory.GetFiles(stateDir, "*.json");
        return files.Select(Path.GetFileNameWithoutExtension).ToList();
    }

    /// <summary>
    /// Delete a repository index.
    /// </summary>
    public async Task DeleteIndexAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        string collectionName = SanitizeCollectionName(repositoryName);
        await qdrantService.DeleteCollectionAsync(collectionName, cancellationToken);

        string statePath = GetIndexStatePath(repositoryName);
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        logger.LogInformation("Deleted index for repository {Repository}", repositoryName);
    }

    #region Private Helpers

    private static List<string> DiscoverFiles(
        string repositoryPath,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<string> excludePatterns)
    {
        var matcher = new Matcher();

        foreach (string pattern in includePatterns)
        {
            matcher.AddInclude(pattern);
        }

        foreach (string pattern in excludePatterns)
        {
            matcher.AddExclude(pattern);
        }

        PatternMatchingResult result = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(
            new DirectoryInfo(repositoryPath)));

        return result.Files.Select(f => f.Path).ToList();
    }

    private static (List<string> toAdd, List<string> toUpdate, List<string> toRemove, List<string> toSkip)
        CategorizeFiles(
            List<string> currentFiles,
            Dictionary<string, IndexedFile> existingFiles,
            string repositoryPath)
    {
        var toAdd = new List<string>();
        var toUpdate = new List<string>();
        var toSkip = new List<string>();
        HashSet<string> currentFileSet = currentFiles.ToHashSet();

        foreach (string relativePath in currentFiles)
        {
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
            .Where(f => !currentFileSet.Contains(f))
            .ToList();

        return (toAdd, toUpdate, toRemove, toSkip);
    }

    private static string ComputeFileHash(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string SanitizeCollectionName(string name)
    {
        // Qdrant collection names must be alphanumeric with underscores
        return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).ToLowerInvariant();
    }

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

    private string GetIndexStatePath(string repositoryName)
    {
        string safeFileName = SanitizeCollectionName(repositoryName);
        return Path.Combine(_options.IndexStateDirectory, $"{safeFileName}.json");
    }

    private async Task<IndexStateFile?> LoadIndexStateAsync(string repositoryName, CancellationToken cancellationToken)
    {
        string path = GetIndexStatePath(repositoryName);
        if (!File.Exists(path)) return null;

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<IndexStateFile>(json);
    }

    private async Task SaveIndexStateAsync(string repositoryName, IndexStateFile state, CancellationToken cancellationToken)
    {
        string path = GetIndexStatePath(repositoryName);
        string dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    #endregion

    /// <summary>
    /// Internal class for persisting index state to disk.
    /// </summary>
    private sealed class IndexStateFile
    {
        public required string RepositoryName { get; init; }
        public required string RootPath { get; init; }
        public string? LastCommitSha { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset LastUpdatedAt { get; init; }
        public required string EmbeddingModel { get; init; }
        public required string CollectionName { get; init; }
        public required List<string> IncludePatterns { get; init; }
        public required List<string> ExcludePatterns { get; init; }
        public required Dictionary<string, IndexedFile> Files { get; init; }
    }
}
