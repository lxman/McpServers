using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RedisBrowser.Core.Services;

namespace RedisMcp.McpTools;

/// <summary>
/// MCP tools for Redis key expiry management
/// </summary>
[McpServerToolType]
public class ExpiryTools(
    ILogger<ExpiryTools> logger,
    RedisService redisService)
{
    [McpServerTool, DisplayName("get_ttl")]
    [Description("Get time-to-live of a Redis key. See skills/redis/expiry/get-ttl.md only when using this tool")]
    public async Task<string> GetTtl(string key)
    {
        try
        {
            logger.LogDebug("Getting TTL for Redis key: {Key}", key);

            string result = await redisService.GetTtlAsync(key);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting TTL for key: {Key}", key);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("set_expire")]
    [Description("Set expiry time for a Redis key. See skills/redis/expiry/set-expire.md only when using this tool")]
    public async Task<string> SetExpire(string key, int expirySeconds)
    {
        try
        {
            logger.LogDebug("Setting expiry for Redis key: {Key}, Seconds: {ExpirySeconds}", key, expirySeconds);

            TimeSpan expiry = TimeSpan.FromSeconds(expirySeconds);
            string result = await redisService.SetExpireAsync(key, expiry);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting expiry for key: {Key}", key);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
