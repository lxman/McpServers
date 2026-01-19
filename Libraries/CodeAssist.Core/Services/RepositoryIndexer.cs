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
public sealed class RepositoryIndexer
{
    private readonly OllamaService _ollamaService;
    private readonly QdrantService _qdrantService;
    private readonly ChunkerFactory _chunkerFactory;
    private readonly CodeAssistOptions _options;
    private readonly ILogger<RepositoryIndexer> _logger;

    private const int EmbeddingBatchSize = 50;

    public RepositoryIndexer(
        OllamaService ollamaService,
        QdrantService qdrantService,
        ChunkerFactory chunkerFactory,
        IOptions<CodeAssistOptions> options,
        ILogger<RepositoryIndexer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _chunkerFactory = chunkerFactory;
        _options = options.Value;
        _logger = logger;
    }

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
        var collectionName = SanitizeCollectionName(repositoryName);

        includePatterns ??= _options.DefaultIncludePatterns;
        excludePatterns ??= _options.DefaultExcludePatterns;

        _logger.LogInformation("Starting indexing of repository {Repository} at {Path}",
            repositoryName, repositoryPath);

        try
        {
            // Ensure embedding model is available
            await _ollamaService.EnsureModelAvailableAsync(cancellationToken);

            _logger.LogDebug("Ensuring collection exists...");
            // Ensure collection exists
            await _qdrantService.EnsureCollectionAsync(collectionName, cancellationToken);

            _logger.LogDebug("Loading index state...");
            // Load existing index state
            var existingState = await LoadIndexStateAsync(repositoryName, cancellationToken);
            var existingFiles = existingState?.Files ?? new Dictionary<string, IndexedFile>();

            _logger.LogDebug("Discovering files...");
            // Discover files to index
            var filesToProcess = DiscoverFiles(repositoryPath, includePatterns, excludePatterns);

            _logger.LogInformation("Found {Count} files to process", filesToProcess.Count);

            // Categorize files
            var (filesToAdd, filesToUpdate, filesToRemove, filesToSkip) =
                CategorizeFiles(filesToProcess, existingFiles, repositoryPath);

            _logger.LogInformation(
                "Files to add: {Add}, update: {Update}, remove: {Remove}, skip: {Skip}",
                filesToAdd.Count, filesToUpdate.Count, filesToRemove.Count, filesToSkip.Count);

            // Remove stale files from index
            foreach (var file in filesToRemove)
            {
                await _qdrantService.DeleteByFilePathAsync(collectionName, file, cancellationToken);
            }

            // Process new and updated files in parallel
            var allChunks = new System.Collections.Concurrent.ConcurrentBag<CodeChunk>();
            var newFileStates = new System.Collections.Concurrent.ConcurrentDictionary<string, IndexedFile>();
            var failedFilesBag = new System.Collections.Concurrent.ConcurrentBag<string>();

            var filesToChunk = filesToAdd.Concat(filesToUpdate).ToList();
            var processedCount = 0;
            var updateHashSet = filesToUpdate.ToHashSet();

            _logger.LogInformation("Processing {Count} files in parallel...", filesToChunk.Count);

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
                    _logger.LogDebug("Processing file: {File}", relativePath);
                    try
                    {
                        var fullPath = Path.Combine(repositoryPath, relativePath);
                        _logger.LogDebug("Reading file: {File}", relativePath);
                        var content = await File.ReadAllTextAsync(fullPath, ct);
                        _logger.LogDebug("Read {Bytes} bytes from {File}", content.Length, relativePath);
                        var fileInfo = new FileInfo(fullPath);

                        // Delete existing chunks if updating (must be sequential for Qdrant)
                        if (updateHashSet.Contains(relativePath))
                        {
                            await _qdrantService.DeleteByFilePathAsync(collectionName, relativePath, ct);
                        }

                        // Chunk the file
                        _logger.LogDebug("Chunking file: {File}", relativePath);
                        var language = _chunkerFactory.GetLanguage(fullPath);
                        var chunker = _chunkerFactory.GetChunker(fullPath);
                        var chunks = chunker.ChunkCode(content, fullPath, relativePath, language);
                        _logger.LogDebug("Created {Count} chunks for {File}", chunks.Count, relativePath);

                        if (chunks.Count > 0)
                        {
                            foreach (var chunk in chunks)
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
                        var count = Interlocked.Increment(ref processedCount);
                        if (count % 10 == 0 || count == filesToChunk.Count)
                        {
                            var stuckFiles = activeFiles
                                .Where(kvp => (DateTime.UtcNow - kvp.Value).TotalSeconds > 2)
                                .Select(kvp => kvp.Key)
                                .Take(5)
                                .ToList();
                            var stuckInfo = stuckFiles.Count > 0
                                ? $" [SLOW: {string.Join(", ", stuckFiles.Select(f => Path.GetFileName(f)))}]"
                                : "";
                            _logger.LogInformation("Chunked {Count}/{Total} ({Chunks} chunks, {Rate:F0}/sec){Stuck}",
                                count, filesToChunk.Count, allChunks.Count, count / chunkSw.Elapsed.TotalSeconds, stuckInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        activeFiles.TryRemove(relativePath, out _);
                        _logger.LogWarning(ex, "Failed to process file {FilePath}", relativePath);
                        failedFilesBag.Add(relativePath);
                    }
                });

            // Transfer failed files from concurrent bag to list
            foreach (var file in failedFilesBag)
            {
                failedFiles.Add(file);
            }

            _logger.LogInformation("Chunking complete: {FileCount} files, {ChunkCount} chunks",
                filesToChunk.Count - failedFiles.Count, allChunks.Count);

            // Embed and store chunks in batches
            var chunkList = allChunks.ToList();
            var totalChunks = 0;
            var totalBatches = (chunkList.Count + EmbeddingBatchSize - 1) / EmbeddingBatchSize;

            _logger.LogInformation("Embedding {ChunkCount} chunks in {BatchCount} batches...",
                chunkList.Count, totalBatches);

            var embedSw = Stopwatch.StartNew();
            for (var i = 0; i < chunkList.Count; i += EmbeddingBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = chunkList.Skip(i).Take(EmbeddingBatchSize).ToList();
                var texts = batch.Select(c => c.Content).ToList();

                var batchNum = i / EmbeddingBatchSize + 1;
                if (batchNum % 5 == 0 || batchNum == totalBatches)
                {
                    var embeddedSoFar = i + batch.Count;
                    var rate = embeddedSoFar / embedSw.Elapsed.TotalSeconds;
                    _logger.LogInformation("Embedding batch {BatchNum}/{TotalBatches} ({Rate:F0} chunks/sec)",
                        batchNum, totalBatches, rate);
                }

                var embeddings = await _ollamaService.GetEmbeddingsAsync(texts, cancellationToken);
                await _qdrantService.UpsertChunksAsync(collectionName, batch, embeddings, cancellationToken);

                totalChunks += batch.Count;
            }

            // Preserve unchanged file states
            foreach (var relativePath in filesToSkip)
            {
                if (existingFiles.TryGetValue(relativePath, out var existingFile))
                {
                    newFileStates[relativePath] = existingFile;
                    totalChunks += existingFile.ChunkCount;
                }
            }

            // Save updated index state
            var gitCommit = GetGitCommitSha(repositoryPath);
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

            _logger.LogInformation(
                "Indexing complete in {Duration}. Added: {Added}, Updated: {Updated}, Removed: {Removed}, Chunks: {Chunks}",
                sw.Elapsed, filesToAdd.Count, filesToUpdate.Count, filesToRemove.Count, totalChunks);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing failed for repository {Repository}", repositoryName);

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
        var collectionName = SanitizeCollectionName(repositoryName);

        var queryEmbedding = await _ollamaService.GetEmbeddingAsync(query, cancellationToken);
        var results = await _qdrantService.SearchAsync(
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
        var stateFile = await LoadIndexStateAsync(repositoryName, cancellationToken);
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
    public async Task<List<string>> ListIndexedRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var stateDir = _options.IndexStateDirectory;
        if (!Directory.Exists(stateDir))
        {
            return [];
        }

        var files = Directory.GetFiles(stateDir, "*.json");
        return files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
    }

    /// <summary>
    /// Delete a repository index.
    /// </summary>
    public async Task DeleteIndexAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        var collectionName = SanitizeCollectionName(repositoryName);
        await _qdrantService.DeleteCollectionAsync(collectionName, cancellationToken);

        var statePath = GetIndexStatePath(repositoryName);
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        _logger.LogInformation("Deleted index for repository {Repository}", repositoryName);
    }

    #region Private Helpers

    private List<string> DiscoverFiles(
        string repositoryPath,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<string> excludePatterns)
    {
        var matcher = new Matcher();

        foreach (var pattern in includePatterns)
        {
            matcher.AddInclude(pattern);
        }

        foreach (var pattern in excludePatterns)
        {
            matcher.AddExclude(pattern);
        }

        var result = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(
            new DirectoryInfo(repositoryPath)));

        return result.Files.Select(f => f.Path).ToList();
    }

    private (List<string> toAdd, List<string> toUpdate, List<string> toRemove, List<string> toSkip)
        CategorizeFiles(
            List<string> currentFiles,
            Dictionary<string, IndexedFile> existingFiles,
            string repositoryPath)
    {
        var toAdd = new List<string>();
        var toUpdate = new List<string>();
        var toSkip = new List<string>();
        var currentFileSet = currentFiles.ToHashSet();

        foreach (var relativePath in currentFiles)
        {
            var fullPath = Path.Combine(repositoryPath, relativePath);

            if (!existingFiles.TryGetValue(relativePath, out var existingFile))
            {
                toAdd.Add(relativePath);
                continue;
            }

            // Check if file has changed
            var currentHash = ComputeFileHash(File.ReadAllText(fullPath));
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
        var toRemove = existingFiles.Keys
            .Where(f => !currentFileSet.Contains(f))
            .ToList();

        return (toAdd, toUpdate, toRemove, toSkip);
    }

    private static string ComputeFileHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string SanitizeCollectionName(string name)
    {
        // Qdrant collection names must be alphanumeric with underscores
        return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).ToLowerInvariant();
    }

    private string? GetGitCommitSha(string repositoryPath)
    {
        try
        {
            var gitDir = Path.Combine(repositoryPath, ".git");
            if (!Directory.Exists(gitDir)) return null;

            var headPath = Path.Combine(gitDir, "HEAD");
            if (!File.Exists(headPath)) return null;

            var headContent = File.ReadAllText(headPath).Trim();

            // HEAD might be a direct SHA or a ref
            if (!headContent.StartsWith("ref:"))
            {
                return headContent;
            }

            var refPath = headContent["ref:".Length..].Trim();
            var refFile = Path.Combine(gitDir, refPath);

            if (File.Exists(refFile))
            {
                return File.ReadAllText(refFile).Trim();
            }

            // Check packed-refs
            var packedRefsPath = Path.Combine(gitDir, "packed-refs");
            if (File.Exists(packedRefsPath))
            {
                var lines = File.ReadAllLines(packedRefsPath);
                foreach (var line in lines)
                {
                    if (line.EndsWith(refPath))
                    {
                        return line.Split(' ')[0];
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GetIndexStatePath(string repositoryName)
    {
        var safeFileName = SanitizeCollectionName(repositoryName);
        return Path.Combine(_options.IndexStateDirectory, $"{safeFileName}.json");
    }

    private async Task<IndexStateFile?> LoadIndexStateAsync(string repositoryName, CancellationToken cancellationToken)
    {
        var path = GetIndexStatePath(repositoryName);
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<IndexStateFile>(json);
    }

    private async Task SaveIndexStateAsync(string repositoryName, IndexStateFile state, CancellationToken cancellationToken)
    {
        var path = GetIndexStatePath(repositoryName);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
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
