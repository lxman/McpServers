using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Logging;
using SystemDirectory = System.IO.Directory;

namespace DocumentServer.Core.Services.Lucene;

/// <summary>
/// Manages the lifecycle of Lucene indexes including loading, unloading, and resource management.
/// Provides lazy loading of indexes and memory management capabilities.
/// </summary>
public class IndexManager : IDisposable
{
    private readonly ILogger<IndexManager> _logger;
    private readonly Dictionary<string, IndexResources> _loadedIndexes = new();
    private readonly HashSet<string> _discoveredIndexNames = [];
    private readonly string _indexBasePath;
    private const LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;

    public IndexManager(ILogger<IndexManager> logger)
    {
        _logger = logger;

        // Default index path - can be made configurable
        _indexBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DocumentServer", "Indexes");

        SystemDirectory.CreateDirectory(_indexBasePath);
        DiscoverExistingIndexes();
    }

    /// <summary>
    /// Discovers existing indexes on disk at startup.
    /// </summary>
    private void DiscoverExistingIndexes()
    {
        try
        {
            if (!SystemDirectory.Exists(_indexBasePath))
            {
                _logger.LogDebug("Indexes directory does not exist: {IndexesPath}", _indexBasePath);
                return;
            }

            string[] indexDirectories = SystemDirectory.GetDirectories(_indexBasePath);
            foreach (string indexDir in indexDirectories)
            {
                string indexName = Path.GetFileName(indexDir);

                // Verify it looks like a Lucene index (has segments files)
                bool hasSegments = SystemDirectory.GetFiles(indexDir, "segments*").Any();
                if (hasSegments)
                {
                    _discoveredIndexNames.Add(indexName);
                    _logger.LogDebug("Discovered existing index: {IndexName}", indexName);
                }
            }

            if (_discoveredIndexNames.Count != 0)
            {
                _logger.LogInformation("Discovered {Count} existing indexes: {IndexNames}",
                    _discoveredIndexNames.Count,
                    string.Join(", ", _discoveredIndexNames.OrderBy(x => x)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover existing indexes");
        }
    }

    /// <summary>
    /// Checks if an index exists (discovered on disk).
    /// </summary>
    public bool IndexExists(string indexName)
    {
        return _discoveredIndexNames.Contains(indexName);
    }

    /// <summary>
    /// Gets the names of all discovered indexes.
    /// </summary>
    public List<string> GetIndexNames()
    {
        return _discoveredIndexNames.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Gets index resources, loading them into memory if needed (lazy loading).
    /// </summary>
    public IndexResources GetIndexResources(string indexName)
    {
        if (!_discoveredIndexNames.Contains(indexName))
        {
            throw new ArgumentException($"Index '{indexName}' not found");
        }

        // Return if already loaded
        if (_loadedIndexes.TryGetValue(indexName, out IndexResources? resources))
        {
            _logger.LogDebug("Using already loaded index: {IndexName}", indexName);
            return resources;
        }

        // Lazy load the index
        _logger.LogInformation("Loading index into memory: {IndexName}", indexName);
        resources = LoadIndex(indexName);
        _loadedIndexes[indexName] = resources;

        return resources;
    }

    /// <summary>
    /// Loads an index into memory.
    /// </summary>
    private IndexResources LoadIndex(string indexName)
    {
        string indexPath = Path.Combine(_indexBasePath, indexName);

        if (!SystemDirectory.Exists(indexPath))
        {
            throw new DirectoryNotFoundException($"Index directory not found: {indexPath}");
        }

        FSDirectory? directory = FSDirectory.Open(indexPath);
        var analyzer = new StandardAnalyzer(LUCENE_VERSION);
        var config = new IndexWriterConfig(LUCENE_VERSION, analyzer);
        var writer = new IndexWriter(directory, config);

        return new IndexResources
        {
            IndexName = indexName,
            Directory = directory,
            Writer = writer,
            Analyzer = analyzer
        };
    }

    /// <summary>
    /// Registers a new index after it has been created.
    /// </summary>
    public void RegisterIndex(string indexName)
    {
        _discoveredIndexNames.Add(indexName);
        _logger.LogInformation("Registered new index: {IndexName}", indexName);
    }

    /// <summary>
    /// Unloads an index from memory while keeping it discoverable.
    /// The index can be lazy-loaded again when needed.
    /// </summary>
    public bool UnloadIndex(string indexName)
    {
        if (_loadedIndexes.TryGetValue(indexName, out IndexResources? resources))
        {
            try
            {
                resources.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing index resources for: {IndexName}", indexName);
            }

            _loadedIndexes.Remove(indexName);

            // Keep in discovered indexes - this is the key difference from RemoveIndex
            _logger.LogInformation("Unloaded index from memory: {IndexName}", indexName);
            return true;
        }

        _logger.LogDebug("Index not loaded in memory: {IndexName}", indexName);
        return false;
    }

    /// <summary>
    /// Unloads all indexes from memory while keeping them discoverable.
    /// </summary>
    public int UnloadAllIndexes()
    {
        List<string> indexesToUnload = _loadedIndexes.Keys.ToList();
        var unloadedCount = 0;

        foreach (string indexName in indexesToUnload)
        {
            if (UnloadIndex(indexName))
            {
                unloadedCount++;
            }
        }

        _logger.LogInformation("Unloaded {Count} indexes from memory", unloadedCount);
        return unloadedCount;
    }

    /// <summary>
    /// Gets the memory status of all indexes.
    /// </summary>
    public Dictionary<string, IndexMemoryStatus> GetIndexMemoryStatus()
    {
        var status = new Dictionary<string, IndexMemoryStatus>();

        foreach (string indexName in _discoveredIndexNames)
        {
            status[indexName] = new IndexMemoryStatus
            {
                IndexName = indexName,
                IsDiscovered = true,
                IsLoadedInMemory = _loadedIndexes.ContainsKey(indexName),
                EstimatedMemoryUsageMb = _loadedIndexes.ContainsKey(indexName)
                    ? GetEstimatedMemoryUsage(indexName)
                    : 0
            };
        }

        return status;
    }

    /// <summary>
    /// Completely removes an index from the system (both memory and discovery).
    /// This does NOT delete the index files from disk.
    /// Use DeleteIndex() to also remove files from disk.
    /// </summary>
    public bool RemoveIndex(string indexName)
    {
        // First unload from memory if loaded
        UnloadIndex(indexName);

        // Then remove from discovery
        bool removed = _discoveredIndexNames.Remove(indexName);

        if (removed)
        {
            _logger.LogInformation("Completely removed index from tracking: {IndexName}", indexName);
        }

        return removed;
    }

    /// <summary>
    /// Deletes an index completely, including files on disk.
    /// </summary>
    public bool DeleteIndex(string indexName)
    {
        try
        {
            // First remove from memory and tracking
            RemoveIndex(indexName);

            // Then delete files from disk
            string indexPath = Path.Combine(_indexBasePath, indexName);
            if (SystemDirectory.Exists(indexPath))
            {
                SystemDirectory.Delete(indexPath, recursive: true);
                _logger.LogInformation("Deleted index from disk: {IndexName}", indexName);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete index: {IndexName}", indexName);
            throw;
        }
    }

    /// <summary>
    /// Estimates the memory usage of a loaded index based on its disk size.
    /// </summary>
    private double GetEstimatedMemoryUsage(string indexName)
    {
        if (!_loadedIndexes.ContainsKey(indexName))
        {
            return 0.0;
        }

        try
        {
            string indexPath = Path.Combine(_indexBasePath, indexName);

            if (SystemDirectory.Exists(indexPath))
            {
                string[] indexFiles = SystemDirectory.GetFiles(indexPath);
                long totalBytes = indexFiles.Sum(f => new FileInfo(f).Length);
                return totalBytes / (1024.0 * 1024.0); // Convert to MB
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate memory usage for index: {IndexName}", indexName);
        }

        return 50.0; // Default estimate if we can't calculate
    }

    /// <summary>
    /// Gets the base path where indexes are stored.
    /// </summary>
    public string GetIndexBasePath() => _indexBasePath;

    /// <summary>
    /// Finds which index (if any) covers a specific directory.
    /// </summary>
    public string? FindIndexForDirectory(string directoryPath)
    {
        // This would require storing index metadata about what directories they cover
        // For now, we'll just check if there's an index with a matching name
        string dirName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar));

        return _discoveredIndexNames.FirstOrDefault(name =>
            name.Contains(dirName, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        foreach (IndexResources resources in _loadedIndexes.Values)
        {
            try
            {
                resources.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing index resources");
            }
        }

        _loadedIndexes.Clear();
        _discoveredIndexNames.Clear();

        _logger.LogInformation("IndexManager disposed");
    }
}

/// <summary>
/// Represents the resources for a loaded Lucene index.
/// </summary>
public class IndexResources : IDisposable
{
    public string IndexName { get; set; } = string.Empty;
    public FSDirectory Directory { get; set; } = null!;
    public IndexWriter Writer { get; set; } = null!;
    public Analyzer Analyzer { get; set; } = null!;

    public void Dispose()
    {
        Writer?.Dispose();
        Analyzer?.Dispose();
        Directory?.Dispose();
    }
}

/// <summary>
/// Information about an index's memory status.
/// </summary>
public class IndexMemoryStatus
{
    public string IndexName { get; set; } = string.Empty;
    public bool IsDiscovered { get; set; }
    public bool IsLoadedInMemory { get; set; }
    public double EstimatedMemoryUsageMb { get; set; }
}
