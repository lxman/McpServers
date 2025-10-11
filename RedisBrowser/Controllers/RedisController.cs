using Microsoft.AspNetCore.Mvc;
using RedisBrowser.Services;

namespace RedisBrowser.Controllers;

/// <summary>
/// Redis operations API
/// </summary>
[ApiController]
[Route("api/redis")]
public class RedisController(RedisService redisService, ILogger<RedisController> logger) : ControllerBase
{
    /// <summary>
    /// Connect to a Redis server
    /// </summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest request)
    {
        logger.LogInformation("Connecting to Redis: {ConnectionString}", request.ConnectionString);
        string result = await redisService.ConnectAsync(request.ConnectionString);
        return Ok(new { result });
    }

    /// <summary>
    /// Disconnect from the current Redis server
    /// </summary>
    [HttpPost("disconnect")]
    public IActionResult Disconnect()
    {
        logger.LogInformation("Disconnecting from Redis");
        string result = redisService.Disconnect();
        return Ok(new { result });
    }

    /// <summary>
    /// Get the current connection status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        string result = redisService.GetConnectionStatus();
        return Ok(new { result });
    }

    /// <summary>
    /// Select a Redis database
    /// </summary>
    [HttpPost("database")]
    public async Task<IActionResult> SelectDatabase([FromBody] SelectDatabaseRequest request)
    {
        logger.LogInformation("Selecting database: {DatabaseNumber}", request.DatabaseNumber);
        string result = await redisService.SelectDatabaseAsync(request.DatabaseNumber);
        return Ok(new { result });
    }

    /// <summary>
    /// Get the value of a key
    /// </summary>
    [HttpGet("keys/{key}")]
    public async Task<IActionResult> Get(string key)
    {
        logger.LogInformation("Getting key: {Key}", key);
        string result = await redisService.GetAsync(key);
        return Ok(new { result });
    }

    /// <summary>
    /// Set a key to a value with optional expiry
    /// </summary>
    [HttpPost("keys")]
    public async Task<IActionResult> Set([FromBody] SetKeyRequest request)
    {
        logger.LogInformation("Setting key: {Key}", request.Key);
        TimeSpan? expiry = request.ExpirySeconds.HasValue 
            ? TimeSpan.FromSeconds(request.ExpirySeconds.Value) 
            : null;
        string result = await redisService.SetAsync(request.Key, request.Value, expiry);
        return Ok(new { result });
    }

    /// <summary>
    /// Delete a key
    /// </summary>
    [HttpDelete("keys/{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        logger.LogInformation("Deleting key: {Key}", key);
        string result = await redisService.DeleteAsync(key);
        return Ok(new { result });
    }

    /// <summary>
    /// Check if a key exists
    /// </summary>
    [HttpGet("keys/{key}/exists")]
    public async Task<IActionResult> Exists(string key)
    {
        string result = await redisService.ExistsAsync(key);
        return Ok(new { result });
    }

    /// <summary>
    /// List keys matching a pattern
    /// </summary>
    [HttpGet("keys")]
    public async Task<IActionResult> GetKeys([FromQuery] string pattern = "*", [FromQuery] int count = 100)
    {
        logger.LogInformation("Getting keys with pattern: {Pattern}, count: {Count}", pattern, count);
        string result = await redisService.GetKeysAsync(pattern, count);
        return Ok(new { result });
    }

    /// <summary>
    /// Get the data type of a key
    /// </summary>
    [HttpGet("keys/{key}/type")]
    public async Task<IActionResult> GetKeyType(string key)
    {
        string result = await redisService.GetKeyTypeAsync(key);
        return Ok(new { result });
    }

    /// <summary>
    /// Get the time-to-live of a key
    /// </summary>
    [HttpGet("keys/{key}/ttl")]
    public async Task<IActionResult> GetTtl(string key)
    {
        string result = await redisService.GetTtlAsync(key);
        return Ok(new { result });
    }

    /// <summary>
    /// Set an expiry time on a key
    /// </summary>
    [HttpPost("keys/{key}/expire")]
    public async Task<IActionResult> SetExpire(string key, [FromBody] SetExpireRequest request)
    {
        logger.LogInformation("Setting expiry on key: {Key}, seconds: {Seconds}", key, request.Seconds);
        string result = await redisService.SetExpireAsync(key, TimeSpan.FromSeconds(request.Seconds));
        return Ok(new { result });
    }

    /// <summary>
    /// Get Redis server information
    /// </summary>
    [HttpGet("info")]
    public async Task<IActionResult> GetInfo([FromQuery] string section = "")
    {
        string result = await redisService.GetInfoAsync(section);
        return Ok(new { result });
    }

    /// <summary>
    /// DANGER: Delete all keys in the current database
    /// </summary>
    [HttpPost("flush")]
    public async Task<IActionResult> FlushDatabase()
    {
        logger.LogWarning("Flushing entire database!");
        string result = await redisService.FlushDatabaseAsync();
        return Ok(new { result });
    }
}

// Request models
public record ConnectRequest(string ConnectionString);
public record SelectDatabaseRequest(int DatabaseNumber = 0);
public record SetKeyRequest(string Key, string Value, int? ExpirySeconds = null);
public record SetExpireRequest(int Seconds);
