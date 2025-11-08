using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RedisBrowser.Core.Services;

namespace RedisMcp.McpTools;

/// <summary>
/// MCP tools for Redis server operations
/// </summary>
[McpServerToolType]
public class ServerTools(
    ILogger<ServerTools> logger,
    RedisService redisService)
{
    [McpServerTool, DisplayName("get_info")]
    [Description("Get Redis server information. See skills/redis/server/get-info.md only when using this tool")]
    public async Task<string> GetInfo(string? section = null)
    {
        try
        {
            logger.LogDebug("Getting Redis server info, Section: {Section}", section ?? "all");

            var result = await redisService.GetInfoAsync(section ?? "");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting server info");
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("flush_database")]
    [Description("Flush current Redis database. See skills/redis/server/flush-database.md only when using this tool")]
    public async Task<string> FlushDatabase()
    {
        try
        {
            logger.LogDebug("Flushing Redis database");

            var result = await redisService.FlushDatabaseAsync();
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error flushing database");
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
