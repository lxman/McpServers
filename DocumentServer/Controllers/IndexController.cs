using DocumentServer.Models.Common;
using DocumentServer.Models.Requests;
using DocumentServer.Models.Responses;
using DocumentServer.Services.Lucene;
using DocumentServer.Services.Lucene.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentServer.Controllers;

/// <summary>
/// Controller for Lucene index operations (create, search, manage)
/// </summary>
[ApiController]
[Route("api/indexes")]
public class IndexController(
    ILogger<IndexController> logger,
    LuceneIndexer indexer,
    LuceneSearcher searcher,
    IndexManager indexManager)
    : ControllerBase
{
    /// <summary>
    /// Create a new Lucene index from a directory of documents
    /// </summary>
    /// <param name="request">Index creation parameters</param>
    /// <returns>Indexing result with statistics</returns>
    [HttpPost("create")]
    [ProducesResponseType(typeof(ServiceResult<IndexingResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceResult<IndexingResult>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateIndex([FromBody] CreateIndexRequest request)
    {
        logger.LogInformation("Creating index: {IndexName} from {RootPath}", 
            request.IndexName, request.RootPath);

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return BadRequest(ServiceResult<IndexingResult>.CreateFailure("Index name is required"));
        }

        if (string.IsNullOrWhiteSpace(request.RootPath))
        {
            return BadRequest(ServiceResult<IndexingResult>.CreateFailure("Root path is required"));
        }

        ServiceResult<IndexingResult> result = await indexer.BuildIndexAsync(
            request.IndexName,
            request.RootPath,
            request.IncludePatterns,
            request.Recursive);

        if (result.Success)
        {
            logger.LogInformation("Index created successfully: {IndexName}, Indexed: {Count} documents", 
                request.IndexName, result.Data?.IndexedDocuments ?? 0);
            return Ok(result);
        }

        logger.LogWarning("Failed to create index: {IndexName}, Error: {Error}", 
            request.IndexName, result.Error);
        return BadRequest(result);
    }

    /// <summary>
    /// List all available indexes
    /// </summary>
    /// <returns>List of index names and metadata</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IndexListResponse), StatusCodes.Status200OK)]
    public IActionResult ListIndexes()
    {
        logger.LogDebug("Listing all indexes");

        List<string> indexNames = indexManager.GetIndexNames();
        Dictionary<string, IndexMemoryStatus> memoryStatus = indexManager.GetIndexMemoryStatus();

        int loadedCount = memoryStatus.Count(kvp => kvp.Value.IsLoadedInMemory);

        var response = new IndexListResponse
        {
            IndexNames = indexNames,
            TotalCount = indexNames.Count,
            LoadedInMemoryCount = loadedCount
        };

        logger.LogInformation("Found {TotalCount} indexes, {LoadedCount} loaded in memory", 
            response.TotalCount, response.LoadedInMemoryCount);

        return Ok(response);
    }

    /// <summary>
    /// Search a specific index
    /// </summary>
    /// <param name="indexName">Name of the index to search</param>
    /// <param name="request">Search parameters</param>
    /// <returns>Search results</returns>
    [HttpPost("{indexName}/search")]
    [ProducesResponseType(typeof(LuceneSearchResults), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult SearchIndex(string indexName, [FromBody] SearchIndexRequest request)
    {
        logger.LogInformation("Searching index: {IndexName}, Query: {Query}", indexName, request.Query);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { Error = "Query is required" });
        }

        if (!indexManager.IndexExists(indexName))
        {
            logger.LogWarning("Index not found: {IndexName}", indexName);
            return NotFound(new { Error = $"Index '{indexName}' not found" });
        }

        try
        {
            var searchOptions = new SearchOptions
            {
                Query = request.Query,
                MaxResults = request.MaxResults,
                IncludeSnippets = request.IncludeSnippets,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending,
                FileTypes = request.FileTypes,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            LuceneSearchResults results = searcher.Search(request.Query, indexName, searchOptions);

            logger.LogInformation("Search completed: {IndexName}, Found {TotalHits} hits, Returned {Count} results", 
                indexName, results.TotalHits, results.Results.Count);

            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed: {IndexName}, Query: {Query}", indexName, request.Query);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Test a query without returning full results
    /// </summary>
    /// <param name="indexName">Name of the index to test against</param>
    /// <param name="request">Test query parameters</param>
    /// <returns>Query validation and hit count</returns>
    [HttpPost("{indexName}/test")]
    [ProducesResponseType(typeof(TestQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult TestQuery(string indexName, [FromBody] TestQueryRequest request)
    {
        logger.LogInformation("Testing query in index: {IndexName}, Query: {Query}", indexName, request.Query);

        if (!indexManager.IndexExists(indexName))
        {
            logger.LogWarning("Index not found: {IndexName}", indexName);
            return NotFound(new { Error = $"Index '{indexName}' not found" });
        }

        try
        {
            int totalHits = searcher.TestQuery(request.Query, indexName);

            var response = new TestQueryResponse
            {
                Query = request.Query,
                TotalHits = totalHits,
                IsValid = true
            };

            logger.LogInformation("Test query succeeded: {IndexName}, Query: {Query}, Hits: {TotalHits}", 
                indexName, request.Query, totalHits);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Test query failed: {IndexName}, Query: {Query}", indexName, request.Query);

            var response = new TestQueryResponse
            {
                Query = request.Query,
                TotalHits = 0,
                IsValid = false,
                ErrorMessage = ex.Message
            };

            return Ok(response);
        }
    }

    /// <summary>
    /// Unload an index from memory (keeps it discoverable)
    /// </summary>
    /// <param name="indexName">Name of the index to unload</param>
    /// <returns>Unload result</returns>
    [HttpPost("{indexName}/unload")]
    [ProducesResponseType(typeof(UnloadIndexResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult UnloadIndex(string indexName)
    {
        logger.LogInformation("Unloading index: {IndexName}", indexName);

        if (!indexManager.IndexExists(indexName))
        {
            logger.LogWarning("Index not found: {IndexName}", indexName);
            return NotFound(new { Error = $"Index '{indexName}' not found" });
        }

        bool success = indexManager.UnloadIndex(indexName);

        var response = new UnloadIndexResponse
        {
            Success = success,
            UnloadedCount = success ? 1 : 0,
            Message = success 
                ? $"Index '{indexName}' unloaded from memory" 
                : $"Index '{indexName}' was not loaded in memory"
        };

        logger.LogInformation("Unload result: {IndexName}, Success: {Success}", indexName, success);

        return Ok(response);
    }

    /// <summary>
    /// Unload all indexes from memory
    /// </summary>
    /// <returns>Unload result with count</returns>
    [HttpDelete("unload-all")]
    [ProducesResponseType(typeof(UnloadIndexResponse), StatusCodes.Status200OK)]
    public IActionResult UnloadAllIndexes()
    {
        logger.LogInformation("Unloading all indexes from memory");

        int unloadedCount = indexManager.UnloadAllIndexes();

        var response = new UnloadIndexResponse
        {
            Success = true,
            UnloadedCount = unloadedCount,
            Message = $"Unloaded {unloadedCount} indexes from memory"
        };

        logger.LogInformation("Unloaded all indexes: {Count} total", unloadedCount);

        return Ok(response);
    }

    /// <summary>
    /// Delete an index completely (from memory and disk)
    /// </summary>
    /// <param name="indexName">Name of the index to delete</param>
    /// <returns>Delete result</returns>
    [HttpDelete("{indexName}")]
    [ProducesResponseType(typeof(DeleteIndexResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult DeleteIndex(string indexName)
    {
        logger.LogInformation("Deleting index: {IndexName}", indexName);

        if (!indexManager.IndexExists(indexName))
        {
            logger.LogWarning("Index not found: {IndexName}", indexName);
            return NotFound(new DeleteIndexResponse
            {
                Success = false,
                IndexName = indexName,
                Message = $"Index '{indexName}' not found"
            });
        }

        try
        {
            bool success = indexManager.DeleteIndex(indexName);

            var response = new DeleteIndexResponse
            {
                Success = success,
                IndexName = indexName,
                Message = success 
                    ? $"Index '{indexName}' deleted successfully" 
                    : $"Failed to delete index '{indexName}'"
            };

            if (success)
            {
                logger.LogInformation("Index deleted: {IndexName}", indexName);
                return Ok(response);
            }

            logger.LogWarning("Failed to delete index: {IndexName}", indexName);
            return StatusCode(StatusCodes.Status500InternalServerError, response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting index: {IndexName}", indexName);
            return StatusCode(StatusCodes.Status500InternalServerError, new DeleteIndexResponse
            {
                Success = false,
                IndexName = indexName,
                Message = $"Error deleting index: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get memory usage information for all indexes
    /// </summary>
    /// <returns>Memory status for each index</returns>
    [HttpGet("memory")]
    [ProducesResponseType(typeof(Dictionary<string, IndexMemoryStatus>), StatusCodes.Status200OK)]
    public IActionResult GetMemoryStatus()
    {
        logger.LogDebug("Getting memory status for all indexes");

        Dictionary<string, IndexMemoryStatus> memoryStatus = indexManager.GetIndexMemoryStatus();

        logger.LogInformation("Memory status retrieved: {Count} indexes tracked", memoryStatus.Count);

        return Ok(memoryStatus);
    }

    /// <summary>
    /// Find which index (if any) covers a specific directory
    /// </summary>
    /// <param name="directoryPath">Directory path to check</param>
    /// <returns>Index name if found, otherwise null</returns>
    [HttpGet("find-for-directory")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public IActionResult FindIndexForDirectory([FromQuery] string directoryPath)
    {
        logger.LogDebug("Finding index for directory: {DirectoryPath}", directoryPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return BadRequest(new { Error = "Directory path is required" });
        }

        string? indexName = indexManager.FindIndexForDirectory(directoryPath);

        var response = new Dictionary<string, string>
        {
            ["directoryPath"] = directoryPath,
            ["indexName"] = indexName ?? "none"
        };

        logger.LogInformation("Index lookup for directory: {DirectoryPath}, Found: {IndexName}", 
            directoryPath, indexName ?? "none");

        return Ok(response);
    }
}
