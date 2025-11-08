using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MongoServer.Core;

namespace MongoMcp.McpTools;

/// <summary>
/// MCP tools for MongoDB database management
/// </summary>
[McpServerToolType]
public class DatabaseTools(
    ILogger<DatabaseTools> logger,
    MongoDbService mongoService)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_databases")]
    [Description("List all databases on a MongoDB server. See skills/mongo/database/list-databases.md only when using this tool")]
    public async Task<string> ListDatabases(string serverName)
    {
        try
        {
            logger.LogDebug("Listing databases on server: {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            var result = await mongoService.ListDatabasesAsync(serverName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing databases on server: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("switch_database")]
    [Description("Switch to a different database on a MongoDB server. See skills/mongo/database/switch-database.md only when using this tool")]
    public async Task<string> SwitchDatabase(string serverName, string databaseName)
    {
        try
        {
            logger.LogDebug("Switching to database {DatabaseName} on server {ServerName}", databaseName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Database name is required" }, _jsonOptions);
            }

            var result = await mongoService.SwitchDatabaseAsync(serverName, databaseName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error switching database on server: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_current_database_info")]
    [Description("Get information about the current database. See skills/mongo/database/get-current-database-info.md only when using this tool")]
    public string GetCurrentDatabaseInfo(string serverName)
    {
        try
        {
            logger.LogDebug("Getting current database info for server: {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            var result = mongoService.GetCurrentDatabaseInfo(serverName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current database info: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_collections_by_database")]
    [Description("List all collections in a specific database. See skills/mongo/database/list-collections-by-database.md only when using this tool")]
    public async Task<string> ListCollectionsByDatabase(string serverName, string databaseName)
    {
        try
        {
            logger.LogDebug("Listing collections in database {DatabaseName} on server {ServerName}", databaseName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Database name is required" }, _jsonOptions);
            }

            var result = await mongoService.ListCollectionsByDatabaseAsync(serverName, databaseName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing collections in database {DatabaseName} on server: {ServerName}", databaseName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
