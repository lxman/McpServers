using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MongoServer.Core;

namespace MongoTools.Controllers;

/// <summary>
/// Manages MongoDB database operations and navigation
/// </summary>
[ApiController]
[Route("api/database")]
[Tags("Database")]
public class DatabaseController(MongoDbService mongoDbService, ILogger<DatabaseController> logger)
    : ControllerBase
{
    /// <summary>
    /// List all databases available on a connected MongoDB server
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListDatabases([FromQuery] string serverName = "default")
    {
        try
        {
            string result = await mongoDbService.ListDatabasesAsync(serverName);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list databases on server: {ServerName}", serverName);
            return StatusCode(500, new
            {
                success = false,
                operation = "list_databases",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = $"Ensure server '{serverName}' is connected. Check connection status first."
            });
        }
    }

    /// <summary>
    /// Switch to a different database on the same MongoDB server
    /// </summary>
    [HttpPost("switch")]
    public async Task<IActionResult> SwitchDatabase([FromBody] SwitchDatabaseRequest request)
    {
        try
        {
            string result = await mongoDbService.SwitchDatabaseAsync(
                request.ServerName ?? "default", 
                request.DatabaseName);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to switch database to {DatabaseName} on {ServerName}", 
                request.DatabaseName, request.ServerName);
            return BadRequest(new
            {
                success = false,
                operation = "switch_database",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                targetDatabase = request.DatabaseName,
                suggestion = "Use GET /api/database/list to see available databases on this server"
            });
        }
    }

    /// <summary>
    /// Get current database information and switching capabilities for a connected server
    /// </summary>
    [HttpGet("current")]
    public IActionResult GetCurrentDatabaseInfo([FromQuery] string serverName = "default")
    {
        try
        {
            string result = mongoDbService.GetCurrentDatabaseInfo(serverName);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get current database info for {ServerName}", serverName);
            return StatusCode(500, new
            {
                success = false,
                operation = "get_current_database_info",
                error = ex.Message,
                operatedOn = serverName
            });
        }
    }

    /// <summary>
    /// Query a specific database and collection on a connected server (allows cross-database queries without switching)
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> QueryDatabase([FromBody] QueryDatabaseRequest request)
    {
        try
        {
            string result = await mongoDbService.QueryDatabaseAsync(
                request.ServerName ?? "default",
                request.DatabaseName,
                request.CollectionName,
                request.Filter ?? "{}",
                request.Limit ?? 100,
                request.Skip ?? 0);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query database {DatabaseName}.{CollectionName}", 
                request.DatabaseName, request.CollectionName);
            return StatusCode(500, new
            {
                success = false,
                operation = "query_database",
                error = ex.Message,
                operatedOn = request.ServerName ?? "default",
                targetDatabase = request.DatabaseName,
                suggestion = "Verify server connection and use GET /api/database/list to see available databases"
            });
        }
    }

    /// <summary>
    /// List collections in a specific database on a connected server (allows browsing any database without switching)
    /// </summary>
    [HttpGet("{databaseName}/collections")]
    public async Task<IActionResult> ListCollectionsByDatabase(
        string databaseName,
        [FromQuery] string serverName = "default")
    {
        try
        {
            string result = await mongoDbService.ListCollectionsByDatabaseAsync(serverName, databaseName);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list collections for database {DatabaseName} on {ServerName}", 
                databaseName, serverName);
            return StatusCode(500, new
            {
                success = false,
                operation = "list_collections_by_database",
                error = ex.Message,
                operatedOn = serverName,
                targetDatabase = databaseName,
                suggestion = "Use GET /api/database/list first to see available databases"
            });
        }
    }

    /// <summary>
    /// Connect with profile and immediately show available databases for browsing
    /// </summary>
    [HttpPost("connect-and-explore")]
    public async Task<IActionResult> ConnectWithProfileAndExplore([FromBody] ConnectWithProfileRequest request)
    {
        try
        {
            // First, connect with the profile
            string connectResult = await mongoDbService.ConnectWithProfileAsync(request.ProfileName);
            
            // Parse the connect result to check if it was successful
            var connectObj = JsonSerializer.Deserialize<JsonElement>(connectResult);
            bool wasSuccessful = !connectResult.Contains("not found") && !connectResult.Contains("incomplete");
            
            if (!wasSuccessful)
            {
                return BadRequest(new
                {
                    success = false,
                    operation = "connect_with_profile_and_explore",
                    error = connectResult,
                    profileName = request.ProfileName,
                    suggestion = "Check that the profile exists using GET /api/connection/profiles"
                });
            }
            
            // Get the server name that was connected
            string serverName = request.ProfileName.Replace(" ", "_").ToLowerInvariant();
            
            // Now list available databases
            string databasesResult = await mongoDbService.ListDatabasesAsync(serverName);
            var databasesObj = JsonSerializer.Deserialize<JsonElement>(databasesResult);
            
            return Ok(new
            {
                success = true,
                operation = "connect_with_profile_and_explore",
                profileName = request.ProfileName,
                serverName,
                connectionResult = connectResult,
                availableDatabases = databasesObj,
                nextSteps = new[]
                {
                    "Use POST /api/database/switch to change to any database",
                    "Use GET /api/database/{databaseName}/collections to browse specific databases",
                    "Use POST /api/database/query to query any database directly"
                },
                workflow = new
                {
                    switchDatabase = $"POST /api/database/switch with body {{ \"databaseName\": \"target_db\", \"serverName\": \"{serverName}\" }}",
                    browseDatabase = $"GET /api/database/{{databaseName}}/collections?serverName={serverName}",
                    queryAnyDatabase = "POST /api/database/query with database and collection names"
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect with profile and explore: {ProfileName}", request.ProfileName);
            return StatusCode(500, new
            {
                success = false,
                operation = "connect_with_profile_and_explore",
                error = ex.Message,
                profileName = request.ProfileName,
                suggestion = "Check that the profile exists"
            });
        }
    }
}

// Request DTOs
public record SwitchDatabaseRequest(
    string DatabaseName,
    string? ServerName = "default"
);

public record QueryDatabaseRequest(
    string DatabaseName,
    string CollectionName,
    string? Filter = "{}",
    int? Limit = 100,
    int? Skip = 0,
    string? ServerName = "default"
);

public record ConnectWithProfileRequest(
    string ProfileName
);