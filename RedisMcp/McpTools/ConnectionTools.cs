using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RedisBrowser.Core.Services;

namespace RedisMcp.McpTools;

/// <summary>
/// MCP tools for Redis connection management
/// </summary>
[McpServerToolType]
public class ConnectionTools(
    ILogger<ConnectionTools> logger,
    RedisService redisService)
{
    [McpServerTool, DisplayName("connect")]
    [Description("Connect to Redis server. See skills/redis/connection/connect.md only when using this tool")]
    public async Task<string> Connect(string connectionString)
    {
        try
        {
            logger.LogDebug("Connecting to Redis server");

            string result = await redisService.ConnectAsync(connectionString);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to Redis");
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("disconnect")]
    [Description("Disconnect from Redis server. See skills/redis/connection/disconnect.md only when using this tool")]
    public string Disconnect()
    {
        try
        {
            logger.LogDebug("Disconnecting from Redis server");

            string result = redisService.Disconnect();
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disconnecting from Redis");
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("get_connection_status")]
    [Description("Get Redis connection status. See skills/redis/connection/get-connection-status.md only when using this tool")]
    public string GetConnectionStatus()
    {
        try
        {
            logger.LogDebug("Getting Redis connection status");

            string result = redisService.GetConnectionStatus();
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting connection status");
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("select_database")]
    [Description("Select Redis database by number. See skills/redis/connection/select-database.md only when using this tool")]
    public async Task<string> SelectDatabase(int databaseNumber)
    {
        try
        {
            logger.LogDebug("Selecting Redis database: {DatabaseNumber}", databaseNumber);

            string result = await redisService.SelectDatabaseAsync(databaseNumber);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error selecting database: {DatabaseNumber}", databaseNumber);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
