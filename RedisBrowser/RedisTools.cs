using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RedisBrowser;

[McpServerToolType]
public class RedisTools
{
    private readonly RedisService _redisService;

    public RedisTools(RedisService redisService)
    {
        _redisService = redisService;
    }

    [McpServerTool]
    [Description("Connect to a Redis server. Requires connection string.")]
    public async Task<string> RedisBrowserConnect(
        [Description("Redis connection string (e.g., 'localhost:6379' or 'localhost:6379,password=mypass')")]
        string connectionString)
    {
        return await _redisService.ConnectAsync(connectionString);
    }

    [McpServerTool]
    [Description("Disconnect from the current Redis server.")]
    public string RedisBrowserDisconnect()
    {
        return _redisService.Disconnect();
    }

    [McpServerTool]
    [Description("Check the current connection status to Redis.")]
    public string RedisBrowserGetConnectionStatus()
    {
        return _redisService.GetConnectionStatus();
    }

    [McpServerTool]
    [Description("Select a Redis database by number (0-15 typically).")]
    public async Task<string> RedisBrowserSelectDatabase(
        [Description("Database number to select (default: 0)")]
        int databaseNumber = 0)
    {
        return await _redisService.SelectDatabaseAsync(databaseNumber);
    }

    [McpServerTool]
    [Description("Get the value of a Redis key.")]
    public async Task<string> RedisBrowserGet(
        [Description("The Redis key to retrieve")]
        string key)
    {
        return await _redisService.GetAsync(key);
    }

    [McpServerTool]
    [Description("Set a Redis key to a string value with optional expiry.")]
    public async Task<string> RedisBrowserSet(
        [Description("The Redis key to set")]
        string key,
        [Description("The value to set")]
        string value,
        [Description("Optional expiry time in seconds")]
        int? expirySeconds = null)
    {
        TimeSpan? expiry = expirySeconds.HasValue ? TimeSpan.FromSeconds(expirySeconds.Value) : null;
        return await _redisService.SetAsync(key, value, expiry);
    }

    [McpServerTool]
    [Description("Delete a Redis key.")]
    public async Task<string> RedisBrowserDelete(
        [Description("The Redis key to delete")]
        string key)
    {
        return await _redisService.DeleteAsync(key);
    }

    [McpServerTool]
    [Description("Check if a Redis key exists.")]
    public async Task<string> RedisBrowserExists(
        [Description("The Redis key to check")]
        string key)
    {
        return await _redisService.ExistsAsync(key);
    }

    [McpServerTool]
    [Description("List Redis keys matching a pattern. Use with caution on large databases.")]
    public async Task<string> RedisBrowserGetKeys(
        [Description("Key pattern to match (default: '*' for all keys)")]
        string pattern = "*",
        [Description("Maximum number of keys to return (default: 100)")]
        int count = 100)
    {
        return await _redisService.GetKeysAsync(pattern, count);
    }

    [McpServerTool]
    [Description("Get the data type of a Redis key.")]
    public async Task<string> RedisBrowserGetKeyType(
        [Description("The Redis key to check")]
        string key)
    {
        return await _redisService.GetKeyTypeAsync(key);
    }

    [McpServerTool]
    [Description("Get the time-to-live (TTL) of a Redis key in seconds.")]
    public async Task<string> RedisBrowserGetTtl(
        [Description("The Redis key to check")]
        string key)
    {
        return await _redisService.GetTtlAsync(key);
    }

    [McpServerTool]
    [Description("Set an expiry time on a Redis key.")]
    public async Task<string> RedisBrowserSetExpire(
        [Description("The Redis key to set expiry on")]
        string key,
        [Description("Expiry time in seconds")]
        int seconds)
    {
        return await _redisService.SetExpireAsync(key, TimeSpan.FromSeconds(seconds));
    }

    [McpServerTool]
    [Description("Get Redis server information and statistics.")]
    public async Task<string> RedisBrowserGetInfo(
        [Description("Optional info section (e.g., 'server', 'memory', 'stats'). Leave empty for all sections.")]
        string section = "")
    {
        return await _redisService.GetInfoAsync(section);
    }

    [McpServerTool]
    [Description("DANGER: Delete all keys in the current database. Use with extreme caution!")]
    public async Task<string> RedisBrowserFlushDatabase()
    {
        return await _redisService.FlushDatabaseAsync();
    }

    [McpServerTool]
    [Description("Show help information about available Redis browser commands.")]
    public static string RedisBrowserGetHelp()
    {
        var commands = new[]
        {
            new { command = "RedisBrowserConnect", description = "Connect to Redis server" },
            new { command = "RedisBrowserDisconnect", description = "Disconnect from Redis" },
            new { command = "RedisBrowserGetConnectionStatus", description = "Check connection status" },
            new { command = "RedisBrowserSelectDatabase", description = "Select database (0-15)" },
            new { command = "RedisBrowserGet", description = "Get key value" },
            new { command = "RedisBrowserSet", description = "Set key value (with optional expiry)" },
            new { command = "RedisBrowserDelete", description = "Delete key" },
            new { command = "RedisBrowserExists", description = "Check if key exists" },
            new { command = "RedisBrowserGetKeys", description = "List keys (with pattern)" },
            new { command = "RedisBrowserGetKeyType", description = "Get key data type" },
            new { command = "RedisBrowserGetTtl", description = "Get key time-to-live" },
            new { command = "RedisBrowserSetExpire", description = "Set key expiry" },
            new { command = "RedisBrowserGetInfo", description = "Get server info" },
            new { command = "RedisBrowserFlushDatabase", description = "DANGER: Delete all keys in database" }
        };

        return System.Text.Json.JsonSerializer.Serialize(new 
        { 
            toolNamespace = "RedisBrowser",
            description = "Redis browser commands for connecting to and browsing Redis databases",
            availableCommands = commands
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
