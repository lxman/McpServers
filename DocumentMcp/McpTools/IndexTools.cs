using System.ComponentModel;
using System.Text.Json;
using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Lucene;
using DocumentServer.Core.Services.Lucene.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DocumentMcp.McpTools;

/// <summary>
/// MCP tools for document indexing operations (create, search, manage indexes)
/// </summary>
[McpServerToolType]
public class IndexTools(
    IndexManager indexManager,
    LuceneIndexer indexer,
    LuceneSearcher searcher,
    ILogger<IndexTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("create_index")]
    [Description("Create a new search index from a directory of documents. See skills/document/index/create-index.md only when using this tool")]
    public async Task<string> CreateIndex(
        string indexName,
        string rootPath,
        string? includePatterns = null,
        bool recursive = true)
    {
        try
        {
            logger.LogInformation("Creating index: {IndexName} from {RootPath}", indexName, rootPath);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Index name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Root path is required" }, _jsonOptions);
            }

            if (!Directory.Exists(rootPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Root path does not exist" }, _jsonOptions);
            }

            var result = await indexer.BuildIndexAsync(
                indexName,
                rootPath,
                includePatterns,
                recursive);

            if (!result.Success)
            {
                logger.LogWarning("Failed to create index: {IndexName}, Error: {Error}", indexName, result.Error);
                return JsonSerializer.Serialize(new { success = false, error = result.Error }, _jsonOptions);
            }

            logger.LogInformation("Index created successfully: {IndexName}, Indexed: {Count} documents",
                indexName, result.Data?.IndexedDocuments ?? 0);

            return JsonSerializer.Serialize(new
            {
                success = true,
                indexName = result.Data?.IndexName,
                rootPath = result.Data?.RootPath,
                startTime = result.Data?.StartTime,
                endTime = result.Data?.EndTime,
                durationMs = result.Data?.Duration.TotalMilliseconds,
                totalDocuments = result.Data?.TotalDocuments ?? 0,
                indexedDocuments = result.Data?.IndexedDocuments ?? 0,
                failedDocuments = result.Data?.FailedDocuments ?? 0
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating index: {IndexName}", indexName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_indexes")]
    [Description("List all available search indexes. See skills/document/index/list-indexes.md only when using this tool")]
    public string ListIndexes()
    {
        try
        {
            logger.LogDebug("Listing all indexes");

            var indexNames = indexManager.GetIndexNames();
            var memoryStatus = indexManager.GetIndexMemoryStatus();

            var loadedCount = memoryStatus.Count(kvp => kvp.Value.IsLoadedInMemory);

            logger.LogInformation("Found {TotalCount} indexes, {LoadedCount} loaded in memory",
                indexNames.Count, loadedCount);

            return JsonSerializer.Serialize(new
            {
                success = true,
                indexNames,
                totalCount = indexNames.Count,
                loadedInMemoryCount = loadedCount,
                indexes = memoryStatus.Select(kvp => new
                {
                    name = kvp.Key,
                    isLoadedInMemory = kvp.Value.IsLoadedInMemory,
                    estimatedMemoryUsageMb = kvp.Value.EstimatedMemoryUsageMb
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing indexes");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("search_index")]
    [Description("Search within a specific index. See skills/document/index/search-index.md only when using this tool")]
    public string SearchIndex(
        string indexName,
        string query,
        int maxResults = 50,
        bool includeSnippets = true,
        string sortBy = "relevance",
        bool sortDescending = true,
        List<string>? fileTypes = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            logger.LogInformation("Searching index: {IndexName}, Query: {Query}", indexName, query);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Index name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Query is required" }, _jsonOptions);
            }

            if (!indexManager.IndexExists(indexName))
            {
                logger.LogWarning("Index not found: {IndexName}", indexName);
                return JsonSerializer.Serialize(new { success = false, error = $"Index '{indexName}' not found" }, _jsonOptions);
            }

            var searchOptions = new SearchOptions
            {
                Query = query,
                MaxResults = maxResults,
                IncludeSnippets = includeSnippets,
                SortBy = sortBy,
                SortDescending = sortDescending,
                FileTypes = fileTypes ?? new List<string>(),
                StartDate = startDate,
                EndDate = endDate
            };

            var results = searcher.Search(query, indexName, searchOptions);

            logger.LogInformation("Search completed: {IndexName}, Found {TotalHits} hits, Returned {Count} results",
                indexName, results.TotalHits, results.Results.Count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                indexName,
                query,
                totalHits = results.TotalHits,
                resultCount = results.Results.Count,
                searchTimeMs = results.SearchTimeMs,
                results = results.Results.Select(r => new
                {
                    filePath = r.FilePath,
                    title = r.Title,
                    documentType = r.DocumentType,
                    relevanceScore = r.RelevanceScore,
                    snippets = r.Snippets,
                    modifiedDate = r.ModifiedDate,
                    fileSizeBytes = r.FileSizeBytes
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching index: {IndexName}, Query: {Query}", indexName, query);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("test_index_query")]
    [Description("Test a query against an index without returning full results. See skills/document/index/test-index.md only when using this tool")]
    public string TestIndexQuery(string indexName, string query)
    {
        try
        {
            logger.LogInformation("Testing query in index: {IndexName}, Query: {Query}", indexName, query);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Index name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Query is required" }, _jsonOptions);
            }

            if (!indexManager.IndexExists(indexName))
            {
                logger.LogWarning("Index not found: {IndexName}", indexName);
                return JsonSerializer.Serialize(new { success = false, error = $"Index '{indexName}' not found" }, _jsonOptions);
            }

            try
            {
                var totalHits = searcher.TestQuery(query, indexName);

                logger.LogInformation("Test query succeeded: {IndexName}, Query: {Query}, Hits: {TotalHits}",
                    indexName, query, totalHits);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    indexName,
                    query,
                    totalHits,
                    isValid = true
                }, _jsonOptions);
            }
            catch (Exception queryEx)
            {
                logger.LogWarning(queryEx, "Test query failed: {IndexName}, Query: {Query}", indexName, query);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    indexName,
                    query,
                    totalHits = 0,
                    isValid = false,
                    errorMessage = queryEx.Message
                }, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing query: {IndexName}, Query: {Query}", indexName, query);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("unload_index")]
    [Description("Unload an index from memory while keeping it discoverable. See skills/document/index/unload.md only when using this tool")]
    public string UnloadIndex(string indexName)
    {
        try
        {
            logger.LogInformation("Unloading index: {IndexName}", indexName);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Index name is required" }, _jsonOptions);
            }

            if (!indexManager.IndexExists(indexName))
            {
                logger.LogWarning("Index not found: {IndexName}", indexName);
                return JsonSerializer.Serialize(new { success = false, error = $"Index '{indexName}' not found" }, _jsonOptions);
            }

            var unloaded = indexManager.UnloadIndex(indexName);

            logger.LogInformation("Unload result: {IndexName}, Success: {Success}", indexName, unloaded);

            return JsonSerializer.Serialize(new
            {
                success = true,
                indexName,
                wasLoaded = unloaded,
                message = unloaded
                    ? $"Index '{indexName}' unloaded from memory"
                    : $"Index '{indexName}' was not loaded in memory"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unloading index: {IndexName}", indexName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("unload_all_indexes")]
    [Description("Unload all indexes from memory. See skills/document/index/unload-all.md only when using this tool")]
    public string UnloadAllIndexes()
    {
        try
        {
            logger.LogInformation("Unloading all indexes from memory");

            var unloadedCount = indexManager.UnloadAllIndexes();

            logger.LogInformation("Unloaded all indexes: {Count} total", unloadedCount);

            return JsonSerializer.Serialize(new
            {
                success = true,
                unloadedCount,
                message = $"Unloaded {unloadedCount} indexes from memory"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unloading all indexes");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_index")]
    [Description("Delete an index completely from memory and disk. See skills/document/index/delete.md only when using this tool")]
    public string DeleteIndex(string indexName)
    {
        try
        {
            logger.LogInformation("Deleting index: {IndexName}", indexName);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Index name is required" }, _jsonOptions);
            }

            if (!indexManager.IndexExists(indexName))
            {
                logger.LogWarning("Index not found: {IndexName}", indexName);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    indexName,
                    message = $"Index '{indexName}' not found"
                }, _jsonOptions);
            }

            var deleted = indexManager.DeleteIndex(indexName);

            logger.LogInformation("Index deleted: {IndexName}, Success: {Success}", indexName, deleted);

            return JsonSerializer.Serialize(new
            {
                success = deleted,
                indexName,
                message = deleted
                    ? $"Index '{indexName}' deleted successfully"
                    : $"Failed to delete index '{indexName}'"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting index: {IndexName}", indexName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                indexName,
                message = $"Error deleting index: {ex.Message}"
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_index_memory_status")]
    [Description("Get memory usage information for all indexes. See skills/document/index/get-memory-status.md only when using this tool")]
    public string GetIndexMemoryStatus()
    {
        try
        {
            logger.LogDebug("Getting memory status for all indexes");

            var memoryStatus = indexManager.GetIndexMemoryStatus();

            var totalIndexes = memoryStatus.Count;
            var loadedIndexes = memoryStatus.Count(kvp => kvp.Value.IsLoadedInMemory);
            var totalMemoryMb = memoryStatus.Sum(kvp => kvp.Value.EstimatedMemoryUsageMb);

            logger.LogInformation("Memory status retrieved: {Count} indexes tracked, {Loaded} loaded, {MemoryMb:F2} MB total",
                totalIndexes, loadedIndexes, totalMemoryMb);

            return JsonSerializer.Serialize(new
            {
                success = true,
                totalIndexes,
                loadedIndexes,
                totalMemoryMb,
                indexes = memoryStatus.Select(kvp => new
                {
                    indexName = kvp.Key,
                    isDiscovered = kvp.Value.IsDiscovered,
                    isLoadedInMemory = kvp.Value.IsLoadedInMemory,
                    estimatedMemoryUsageMb = kvp.Value.EstimatedMemoryUsageMb
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting memory status");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("find_index_for_directory")]
    [Description("Find which index (if any) covers a specific directory. See skills/document/index/find-for-directory.md only when using this tool")]
    public string FindIndexForDirectory(string directoryPath)
    {
        try
        {
            logger.LogDebug("Finding index for directory: {DirectoryPath}", directoryPath);

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Directory path is required" }, _jsonOptions);
            }

            var indexName = indexManager.FindIndexForDirectory(directoryPath);

            logger.LogInformation("Index lookup for directory: {DirectoryPath}, Found: {IndexName}",
                directoryPath, indexName ?? "none");

            return JsonSerializer.Serialize(new
            {
                success = true,
                directoryPath,
                indexName = indexName ?? "none",
                found = indexName != null
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding index for directory: {DirectoryPath}", directoryPath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
