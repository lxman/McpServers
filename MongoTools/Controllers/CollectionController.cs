using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MongoTools.Controllers;

/// <summary>
/// Manages MongoDB collection operations
/// </summary>
[ApiController]
[Route("api/collection")]
[Tags("Collection")]
public class CollectionController(MongoDbService mongoDbService, ILogger<CollectionController> logger)
    : ControllerBase
{
    /// <summary>
    /// List all collections in a database
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListCollections([FromQuery] string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default" && !mongoDbService.ConnectionManager.IsConnected("default"))
        {
            return BadRequest(new
            {
                success = false,
                error = "No primary connection established for list_collections operation",
                solution = "Run POST /api/connection/primary to establish your main database connection",
                operatedOn = "none",
                suggestion = "Primary connection is required for collection operations"
            });
        }

        try
        {
            string result = await mongoDbService.ListCollectionsAsync(serverName);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            
            return Ok(new
            {
                success = true,
                operation = "list_collections",
                operatedOn = serverName,
                collections = resultObj.GetProperty("collections")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list collections on {ServerName}", serverName);
            return StatusCode(500, new
            {
                success = false,
                operation = "list_collections",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection is established and healthy"
            });
        }
    }

    /// <summary>
    /// Create an index on a collection to improve query performance
    /// </summary>
    [HttpPost("index")]
    public async Task<IActionResult> CreateIndex([FromBody] CreateIndexRequest request)
    {
        // Pre-operation validation
        if ((request.ServerName ?? "default") == "default" && !mongoDbService.ConnectionManager.IsConnected("default"))
        {
            return BadRequest(new
            {
                success = false,
                error = "No primary connection established for create_index operation",
                solution = "Run POST /api/connection/primary to establish your main database connection",
                operatedOn = "none",
                suggestion = "Primary connection is required for index operations"
            });
        }

        try
        {
            string result = await mongoDbService.CreateIndexAsync(
                request.ServerName ?? "default",
                request.CollectionName,
                request.IndexKeys,
                request.IndexName);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "create_index",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                indexName = resultObj.GetProperty("indexName").GetString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create index on {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "create_index",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure index specification is valid JSON"
            });
        }
    }

    /// <summary>
    /// Drop (delete) an entire collection and all its documents. Use with caution!
    /// </summary>
    [HttpDelete("drop")]
    public async Task<IActionResult> DropCollection([FromBody] DropCollectionRequest request)
    {
        // Pre-operation validation
        if ((request.ServerName ?? "default") == "default" && !mongoDbService.ConnectionManager.IsConnected("default"))
        {
            return BadRequest(new
            {
                success = false,
                error = "No primary connection established for drop_collection operation",
                solution = "Run POST /api/connection/primary to establish your main database connection",
                operatedOn = "none",
                suggestion = "Primary connection is required for drop operations"
            });
        }

        try
        {
            string result = await mongoDbService.DropCollectionAsync(
                request.ServerName ?? "default",
                request.CollectionName);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "drop_collection",
                operatedOn = request.ServerName ?? "default",
                collection = request.CollectionName,
                message = resultObj.GetProperty("message").GetString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to drop collection {Collection}", request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "drop_collection",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure collection name is correct"
            });
        }
    }

    /// <summary>
    /// Execute a raw MongoDB command. For advanced users only.
    /// </summary>
    [HttpPost("command")]
    public async Task<IActionResult> ExecuteCommand([FromBody] ExecuteCommandRequest request)
    {
        // Pre-operation validation
        if ((request.ServerName ?? "default") == "default" && !mongoDbService.ConnectionManager.IsConnected("default"))
        {
            return BadRequest(new
            {
                success = false,
                error = "No primary connection established for execute_command operation",
                solution = "Run POST /api/connection/primary to establish your main database connection",
                operatedOn = "none",
                suggestion = "Primary connection is required for command execution"
            });
        }

        try
        {
            string result = await mongoDbService.ExecuteCommandAsync(
                request.ServerName ?? "default",
                request.Command);
            
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return Ok(new
            {
                success = true,
                operation = "execute_command",
                operatedOn = request.ServerName ?? "default",
                result = resultObj.GetProperty("result")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute command on {ServerName}", request.ServerName);
            return StatusCode(500, new
            {
                success = false,
                operation = "execute_command",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                suggestion = "Verify connection and ensure command is valid MongoDB JSON"
            });
        }
    }
}

// Request DTOs
public record CreateIndexRequest(
    string CollectionName,
    string IndexKeys,
    string? IndexName = null,
    string? ServerName = "default"
);

public record DropCollectionRequest(
    string CollectionName,
    string? ServerName = "default"
);

public record ExecuteCommandRequest(
    string Command,
    string? ServerName = "default"
);