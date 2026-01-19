using System.Collections.Concurrent;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Caching;

/// <summary>
/// L1 in-memory cache for recently changed files.
/// Provides instant access to hot files with pre-computed embeddings.
/// </summary>
public sealed class HotCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CachedFile> _cache = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastAccess = new();
    private readonly OllamaService _embeddingService;
    private readonly ChunkerFactory _chunkerFactory;
    private readonly CodeAssistOptions _options;
    private readonly ILogger<HotCache> _logger;
    private readonly Timer _evictionTimer;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Event fired when a file is updated in the cache with its embeddings ready for L2 promotion.
    /// </summary>
    public event EventHandler<CachePromotionEventArgs>? FileReadyForPromotion;

    public HotCache(
        OllamaService embeddingService,
        ChunkerFactory chunkerFactory,
        IOptions<CodeAssistOptions> options,
        ILogger<HotCache> logger)
    {
        _embeddingService = embeddingService;
        _chunkerFactory = chunkerFactory;
        _options = options.Value;
        _logger = logger;

        // Run eviction every minute
        _evictionTimer = new Timer(
            EvictStaleEntries,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Maximum number of files to keep in L1 cache.
    /// </summary>
    public int MaxCachedFiles => _options.HotCacheMaxFiles > 0 ? _options.HotCacheMaxFiles : 100;

    /// <summary>
    /// Time-to-live for cached entries.
    /// </summary>
    public TimeSpan CacheTtl => _options.HotCacheTtl > TimeSpan.Zero
        ? _options.HotCacheTtl
        : TimeSpan.FromMinutes(30);

    /// <summary>
    /// Number of files currently in the cache.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Check if a file is in the hot cache.
    /// </summary>
    public bool Contains(string filePath) => _cache.ContainsKey(NormalizePath(filePath));

    /// <summary>
    /// Get cached file if present.
    /// </summary>
    public CachedFile? Get(string filePath)
    {
        var key = NormalizePath(filePath);
        if (_cache.TryGetValue(key, out var cached))
        {
            _lastAccess[key] = DateTime.UtcNow;
            return cached;
        }
        return null;
    }

    /// <summary>
    /// Update or add a file to the cache. Parses, chunks, and embeds the content.
    /// </summary>
    public async Task<CachedFile?> UpdateFileAsync(
        string filePath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizePath(filePath);

        await _updateLock.WaitAsync(cancellationToken);
        try
        {
            // Read fresh content from disk
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("File no longer exists, removing from cache: {File}", filePath);
                Remove(filePath);
                return null;
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var relativePath = Path.GetRelativePath(repositoryRoot, filePath);
            var language = _chunkerFactory.GetLanguage(filePath);

            // Check if content actually changed
            if (_cache.TryGetValue(key, out var existing) && existing.ContentHash == ComputeHash(content))
            {
                _logger.LogDebug("File content unchanged, skipping re-embedding: {File}", relativePath);
                _lastAccess[key] = DateTime.UtcNow;
                return existing;
            }

            _logger.LogInformation("Updating L1 cache for: {File}", relativePath);

            // Chunk the file using tree-sitter
            var chunker = _chunkerFactory.GetChunker(filePath);
            var chunks = chunker.ChunkCode(content, filePath, relativePath, language);

            if (chunks.Count == 0)
            {
                _logger.LogDebug("No chunks produced for file: {File}", relativePath);
                return null;
            }

            // Generate embeddings for all chunks
            var embeddings = await EmbedChunksAsync(chunks, cancellationToken);

            var cachedFile = new CachedFile
            {
                FilePath = filePath,
                RelativePath = relativePath,
                RepositoryRoot = repositoryRoot,
                Content = content,
                ContentHash = ComputeHash(content),
                Language = language,
                Chunks = chunks.ToList(),
                Embeddings = embeddings,
                LastModified = File.GetLastWriteTimeUtc(filePath),
                CachedAt = DateTime.UtcNow
            };

            // Store in cache
            _cache[key] = cachedFile;
            _lastAccess[key] = DateTime.UtcNow;

            // Evict if over capacity
            await EvictIfOverCapacityAsync();

            // Fire promotion event for L2 sync
            FileReadyForPromotion?.Invoke(this, new CachePromotionEventArgs(cachedFile));

            _logger.LogDebug("Cached {ChunkCount} chunks for {File}", chunks.Count, relativePath);

            return cachedFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cache for {File}", filePath);
            return null;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    /// Remove a file from the cache.
    /// </summary>
    public void Remove(string filePath)
    {
        var key = NormalizePath(filePath);
        _cache.TryRemove(key, out _);
        _lastAccess.TryRemove(key, out _);
    }

    /// <summary>
    /// Search the hot cache using semantic similarity.
    /// Returns chunks from hot files that match the query.
    /// </summary>
    public async Task<List<HotCacheSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        float minScore = 0.5f,
        CancellationToken cancellationToken = default)
    {
        if (_cache.IsEmpty)
        {
            return [];
        }

        // Get query embedding
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, cancellationToken);

        var results = new List<HotCacheSearchResult>();

        foreach (var (_, cachedFile) in _cache)
        {
            for (var i = 0; i < cachedFile.Chunks.Count; i++)
            {
                var chunk = cachedFile.Chunks[i];
                var embedding = cachedFile.Embeddings[i];

                var score = CosineSimilarity(queryEmbedding, embedding);

                if (score >= minScore)
                {
                    results.Add(new HotCacheSearchResult
                    {
                        Chunk = chunk,
                        Embedding = embedding,
                        Score = score,
                        CachedFile = cachedFile
                    });
                }
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get all cached files for a repository.
    /// </summary>
    public IEnumerable<CachedFile> GetFilesForRepository(string repositoryRoot)
    {
        var normalizedRoot = NormalizePath(repositoryRoot);
        return _cache.Values
            .Where(f => NormalizePath(f.RepositoryRoot) == normalizedRoot);
    }

    /// <summary>
    /// Clear all entries for a repository.
    /// </summary>
    public void ClearRepository(string repositoryRoot)
    {
        var normalizedRoot = NormalizePath(repositoryRoot);
        var keysToRemove = _cache
            .Where(kvp => NormalizePath(kvp.Value.RepositoryRoot) == normalizedRoot)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
            _lastAccess.TryRemove(key, out _);
        }

        _logger.LogInformation("Cleared {Count} files from cache for repository: {Root}",
            keysToRemove.Count, repositoryRoot);
    }

    /// <summary>
    /// Clear the entire cache.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _lastAccess.Clear();
        _logger.LogInformation("Hot cache cleared");
    }

    private async Task<List<float[]>> EmbedChunksAsync(
        IReadOnlyList<CodeChunk> chunks,
        CancellationToken cancellationToken)
    {
        var contents = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GetEmbeddingsAsync(contents, cancellationToken);
        return embeddings.ToList();
    }

    private void EvictStaleEntries(object? state)
    {
        if (_disposed) return;

        var cutoff = DateTime.UtcNow - CacheTtl;
        var staleKeys = _lastAccess
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _cache.TryRemove(key, out _);
            _lastAccess.TryRemove(key, out _);
        }

        if (staleKeys.Count > 0)
        {
            _logger.LogDebug("Evicted {Count} stale entries from hot cache", staleKeys.Count);
        }
    }

    private async Task EvictIfOverCapacityAsync()
    {
        if (_cache.Count <= MaxCachedFiles) return;

        // Evict least recently accessed entries
        var toEvict = _lastAccess
            .OrderBy(kvp => kvp.Value)
            .Take(_cache.Count - MaxCachedFiles)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toEvict)
        {
            _cache.TryRemove(key, out _);
            _lastAccess.TryRemove(key, out _);
        }

        _logger.LogDebug("Evicted {Count} entries due to capacity", toEvict.Count);
        await Task.CompletedTask;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private static string ComputeHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _evictionTimer.Dispose();
        _updateLock.Dispose();
    }
}

/// <summary>
/// Represents a file cached in L1 with its chunks and embeddings.
/// </summary>
public class CachedFile
{
    public required string FilePath { get; init; }
    public required string RelativePath { get; init; }
    public required string RepositoryRoot { get; init; }
    public required string Content { get; init; }
    public required string ContentHash { get; init; }
    public required string Language { get; init; }
    public required List<CodeChunk> Chunks { get; init; }
    public required List<float[]> Embeddings { get; init; }
    public required DateTime LastModified { get; init; }
    public required DateTime CachedAt { get; init; }
}

/// <summary>
/// Search result from the hot cache.
/// </summary>
public class HotCacheSearchResult
{
    public required CodeChunk Chunk { get; init; }
    public required float[] Embedding { get; init; }
    public required float Score { get; init; }
    public required CachedFile CachedFile { get; init; }
}

/// <summary>
/// Event args for when a file is ready to be promoted to L2.
/// </summary>
public class CachePromotionEventArgs : EventArgs
{
    public CachedFile CachedFile { get; }

    public CachePromotionEventArgs(CachedFile cachedFile)
    {
        CachedFile = cachedFile;
    }
}
