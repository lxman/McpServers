using System.Collections.Concurrent;
using DocumentServer.Core.Models.Common;
using Microsoft.Extensions.Logging;

namespace DocumentServer.Core.Services.Core;

/// <summary>
/// Thread-safe cache for loaded documents with LRU eviction policy
/// </summary>
public class DocumentCache
{
    private readonly ILogger<DocumentCache> _logger;
    private readonly ConcurrentDictionary<string, LoadedDocument> _documents;
    private readonly ConcurrentDictionary<string, DateTime> _accessTimes;
    private readonly int _maxDocuments;
    private readonly long _maxMemoryBytes;
    private readonly SemaphoreSlim _evictionLock;

    /// <summary>
    /// Initializes a new instance of the DocumentCache
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="maxDocuments">Maximum number of documents to cache (default: 50)</param>
    /// <param name="maxMemoryMb">Maximum memory usage in MB (default: 2048MB = 2GB)</param>
    public DocumentCache(ILogger<DocumentCache> logger, int maxDocuments = 50, int maxMemoryMb = 2048)
    {
        _logger = logger;
        _documents = new ConcurrentDictionary<string, LoadedDocument>(StringComparer.OrdinalIgnoreCase);
        _accessTimes = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        _maxDocuments = maxDocuments;
        _maxMemoryBytes = maxMemoryMb * 1024L * 1024L;
        _evictionLock = new SemaphoreSlim(1, 1);

        _logger.LogInformation("DocumentCache initialized: MaxDocuments={MaxDocuments}, MaxMemoryMB={MaxMemoryMB}", 
            maxDocuments, maxMemoryMb);
    }

    /// <summary>
    /// Add or update a document in the cache
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="document">The loaded document to cache</param>
    /// <returns>True if added/updated successfully, false if eviction failed</returns>
    public async Task<bool> AddAsync(string filePath, LoadedDocument document)
    {
        var normalizedPath = NormalizePath(filePath);
        
        _logger.LogDebug("Adding document to cache: {FilePath}", filePath);

        // Check if we need to evict documents
        if (_documents.Count >= _maxDocuments || GetTotalMemoryUsage() + document.MemorySizeBytes > _maxMemoryBytes)
        {
            _logger.LogInformation("Cache limit reached, attempting eviction before adding: {FilePath}", filePath);
            var evicted = await EvictLeastRecentlyUsedAsync();
            
            if (!evicted && _documents.Count >= _maxDocuments)
            {
                _logger.LogWarning("Failed to evict documents, cache full: {Count}/{Max}", 
                    _documents.Count, _maxDocuments);
                return false;
            }
        }

        document.LastAccessedAt = DateTime.UtcNow;
        document.AccessCount = 0;
        
        _documents[normalizedPath] = document;
        _accessTimes[normalizedPath] = DateTime.UtcNow;

        _logger.LogInformation("Document added to cache: {FilePath}, Type={Type}, Size={Size} bytes", 
            filePath, document.DocumentType, document.MemorySizeBytes);

        return true;
    }

    /// <summary>
    /// Retrieve a document from the cache
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>The cached document if found, otherwise null</returns>
    public LoadedDocument? Get(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);

        if (_documents.TryGetValue(normalizedPath, out var document))
        {
            // Update access tracking
            document.LastAccessedAt = DateTime.UtcNow;
            document.AccessCount++;
            _accessTimes[normalizedPath] = DateTime.UtcNow;

            _logger.LogDebug("Cache hit for: {FilePath}, AccessCount={Count}", filePath, document.AccessCount);
            return document;
        }

        _logger.LogDebug("Cache miss for: {FilePath}", filePath);
        return null;
    }

    /// <summary>
    /// Check if a document is in the cache
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>True if the document is cached, otherwise false</returns>
    public bool Contains(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        return _documents.ContainsKey(normalizedPath);
    }

    /// <summary>
    /// Remove a specific document from the cache
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>True if removed, false if not found</returns>
    public bool Remove(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);

        var removed = _documents.TryRemove(normalizedPath, out var document);
        if (removed)
        {
            _accessTimes.TryRemove(normalizedPath, out _);
            _logger.LogInformation("Document removed from cache: {FilePath}, Type={Type}", 
                filePath, document?.DocumentType);
        }
        else
        {
            _logger.LogDebug("Document not found in cache for removal: {FilePath}", filePath);
        }

        return removed;
    }

    /// <summary>
    /// Clear all documents from the cache
    /// </summary>
    public void Clear()
    {
        var count = _documents.Count;
        _documents.Clear();
        _accessTimes.Clear();
        
        _logger.LogInformation("Cache cleared: {Count} documents removed", count);
    }

    /// <summary>
    /// Get list of all cached document paths
    /// </summary>
    /// <returns>List of file paths currently in the cache</returns>
    public List<string> GetCachedPaths()
    {
        return _documents.Keys.ToList();
    }

    /// <summary>
    /// Get the number of documents currently in the cache
    /// </summary>
    /// <returns>Number of cached documents</returns>
    public int GetCount()
    {
        return _documents.Count;
    }

    /// <summary>
    /// Get total memory usage of all cached documents
    /// </summary>
    /// <returns>Total memory usage in bytes</returns>
    public long GetTotalMemoryUsage()
    {
        return _documents.Values.Sum(d => d.MemorySizeBytes);
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    /// <returns>Dictionary containing cache statistics</returns>
    public Dictionary<string, object> GetStatistics()
    {
        var stats = new Dictionary<string, object>
        {
            ["DocumentCount"] = _documents.Count,
            ["MaxDocuments"] = _maxDocuments,
            ["MemoryUsageBytes"] = GetTotalMemoryUsage(),
            ["MaxMemoryBytes"] = _maxMemoryBytes,
            ["MemoryUsageMB"] = GetTotalMemoryUsage() / (1024.0 * 1024.0),
            ["MaxMemoryMB"] = _maxMemoryBytes / (1024.0 * 1024.0),
            ["MemoryUsagePercent"] = (GetTotalMemoryUsage() * 100.0) / _maxMemoryBytes,
            ["DocumentTypes"] = _documents.Values
                .GroupBy(d => d.DocumentType)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };

        return stats;
    }

    /// <summary>
    /// Evict the least recently used document(s) from the cache
    /// </summary>
    /// <returns>True if at least one document was evicted</returns>
    private async Task<bool> EvictLeastRecentlyUsedAsync()
    {
        await _evictionLock.WaitAsync();
        try
        {
            if (_accessTimes.IsEmpty)
            {
                _logger.LogWarning("No documents to evict");
                return false;
            }

            // Find the least recently used document
            var lruEntry = _accessTimes.OrderBy(kvp => kvp.Value).First();
            var pathToEvict = lruEntry.Key;

            if (!_documents.TryRemove(pathToEvict, out var evictedDoc)) return false;
            _accessTimes.TryRemove(pathToEvict, out _);
                
            _logger.LogInformation("Evicted LRU document: {FilePath}, Type={Type}, LastAccessed={LastAccessed}, AccessCount={Count}",
                pathToEvict, evictedDoc.DocumentType, lruEntry.Value, evictedDoc.AccessCount);
                
            return true;

        }
        finally
        {
            _evictionLock.Release();
        }
    }

    /// <summary>
    /// Normalize the file path for consistent cache key lookups
    /// </summary>
    private static string NormalizePath(string filePath)
    {
        try
        {
            return Path.GetFullPath(filePath);
        }
        catch
        {
            // If path normalization fails, use the original path
            return filePath;
        }
    }
}
