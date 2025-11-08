using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RedisBrowser.Core.Services;

namespace RedisMcp.McpTools;

/// <summary>
/// MCP tools for Redis key operations
/// </summary>
[McpServerToolType]
public class KeyTools(
    ILogger<KeyTools> logger,
    RedisService redisService)
{
    [McpServerTool, DisplayName("get")]
    [Description("Get value of a Redis key. See skills/redis/key/get.md only when using this tool")]
    public async Task<string> Get(string key)
    {
        try
        {
            logger.LogDebug("Getting Redis key: {Key}", key);

            var result = await redisService.GetAsync(key);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting key: {Key}", key);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("set")]
    [Description("Set value of a Redis key. See skills/redis/key/set.md only when using this tool")]
    public async Task<string> Set(string key, string value, int? expirySeconds = null)
    {
        try
        {
            logger.LogDebug("Setting Redis key: {Key}", key);

            TimeSpan? expiry = expirySeconds.HasValue ? TimeSpan.FromSeconds(expirySeconds.Value) : null;
            var result = await redisService.SetAsync(key, value, expiry);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting key: {Key}", key);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("delete")]
    [Description("Delete a Redis key. See skills/redis/key/delete.md only when using this tool")]
    public async Task<string> Delete(string key)
    {
        try
        {
            logger.LogDebug("Deleting Redis key: {Key}", key);

            var result = await redisService.DeleteAsync(key);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting key: {Key}", key);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("exists")]
    [Description("Check if a Redis key exists. See skills/redis/key/exists.md only when using this tool")]
    public async Task<string> Exists(string key)
    {
        try
        {
            logger.LogDebug("Checking if Redis key exists: {Key}", key);

            var result = await redisService.ExistsAsync(key);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking key existence: {Key}", key);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("get_keys")]
    [Description("Get Redis keys matching a pattern. See skills/redis/key/get-keys.md only when using this tool")]
    public async Task<string> GetKeys(string pattern = "*", int count = 100)
    {
        try
        {
            logger.LogDebug("Getting Redis keys with pattern: {Pattern}", pattern);

            var result = await redisService.GetKeysAsync(pattern, count);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting keys with pattern: {Pattern}", pattern);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, DisplayName("get_key_type")]
    [Description("Get the type of a Redis key. See skills/redis/key/get-key-type.md only when using this tool")]
    public async Task<string> GetKeyType(string key)
    {
        try
        {
            logger.LogDebug("Getting Redis key type: {Key}", key);

            var result = await redisService.GetKeyTypeAsync(key);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting key type: {Key}", key);
            return System.Text.Json.JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
