using System.Text.Json;
using System.Text.RegularExpressions;
using Mcp.Common.Core.Environment;
using Mcp.Database.Core.Common;
using Mcp.Database.Core.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RedisBrowser.Core.Services;

public class RedisService
{
    private const string DEFAULT_CONNECTION_NAME = "default";
    private readonly RedisConnectionManager _connectionManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisService> _logger;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public RedisService(RedisConnectionManager connectionManager, IConfiguration configuration, ILogger<RedisService> logger)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _configuration = configuration;
        _logger = logger;

        // Try to auto-connect if configured
        _ = Task.Run(TryAutoConnectAsync);
    }

    private async Task TryAutoConnectAsync()
    {
        try
        {
            // Try environment variables first (with registry fallback)
            string? envConnectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("REDIS_CONNECTION_STRING");


            if (!string.IsNullOrEmpty(envConnectionString))
            {
                _logger.LogInformation("Attempting auto-connect using environment variables");
                await ConnectAsync(envConnectionString);
                return;
            }
            
            // Try the configuration file
            string? configConnectionString = _configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(configConnectionString))
            {
                _logger.LogInformation("Attempting auto-connect using configuration file");
                await ConnectAsync(configConnectionString);
                return;
            }
            
            _logger.LogInformation("No auto-connect configuration found");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-connect failed, manual connection will be required");
        }
    }

    public async Task<string> ConnectAsync(string connectionString)
    {
        try
        {
            string result = await _connectionManager.AddConnectionAsync(DEFAULT_CONNECTION_NAME, connectionString, 0);
            int? currentDb = _connectionManager.GetCurrentDatabase(DEFAULT_CONNECTION_NAME);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Successfully connected to Redis",
                database = currentDb ?? 0,
                connectionString = MaskConnectionString(connectionString)
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Connection failed: {ex.Message}"
            }, _options);
        }
    }

    public string Disconnect()
    {
        try
        {
            bool removed = _connectionManager.RemoveConnection(DEFAULT_CONNECTION_NAME);

            return JsonSerializer.Serialize(new
            {
                success = removed,
                message = removed ? "Disconnected from Redis" : "No active connection found"
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public string GetConnectionStatus()
    {
        if (!_connectionManager.IsConnected(DEFAULT_CONNECTION_NAME))
        {
            return JsonSerializer.Serialize(new
            {
                connected = false,
                message = "Not connected to Redis"
            }, _options);
        }

        ConnectionInfo? info = _connectionManager.GetConnectionInfo(DEFAULT_CONNECTION_NAME);
        int? currentDb = _connectionManager.GetCurrentDatabase(DEFAULT_CONNECTION_NAME);

        return JsonSerializer.Serialize(new
        {
            connected = true,
            database = currentDb ?? 0,
            connectionString = info?.ConnectionString != null ? MaskConnectionString(info.ConnectionString) : "unknown",
            isConnected = info?.IsHealthy ?? false
        }, _options);
    }

    private IDatabase EnsureConnected()
    {
        return _connectionManager.GetDatabase(DEFAULT_CONNECTION_NAME)
            ?? throw new InvalidOperationException("Not connected to Redis. Use redis-browser.connect command first.");
    }

    public async Task<string> SelectDatabaseAsync(int databaseNumber)
    {
        try
        {
            string result = await _connectionManager.SelectDatabaseAsync(DEFAULT_CONNECTION_NAME, databaseNumber);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Selected database {databaseNumber}",
                database = databaseNumber
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> GetAsync(string key)
    {
        try
        {
            IDatabase db = EnsureConnected();
            RedisValue value = await db.StringGetAsync(key);
            
            return JsonSerializer.Serialize(new 
            {
                key,
                value = value.HasValue ? (string)value! : null,
                exists = value.HasValue,
                type = "string"
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            IDatabase db = EnsureConnected();
            Expiration expiration = expiry.HasValue ? new Expiration(expiry.Value) : new Expiration(TimeSpan.MaxValue);
            bool result = await db.StringSetAsync(key, value, expiration);
            
            return JsonSerializer.Serialize(new 
            { 
                success = result,
                key,
                message = result ? "Key set successfully" : "Failed to set key",
                expiry = expiry?.TotalSeconds
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> DeleteAsync(string key)
    {
        try
        {
            IDatabase db = EnsureConnected();
            bool result = await db.KeyDeleteAsync(key);
            
            return JsonSerializer.Serialize(new 
            { 
                success = result,
                key,
                message = result ? "Key deleted successfully" : "Key not found"
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> ExistsAsync(string key)
    {
        try
        {
            IDatabase db = EnsureConnected();
            bool exists = await db.KeyExistsAsync(key);
            
            return JsonSerializer.Serialize(new 
            {
                key, exists
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> GetKeysAsync(string pattern = "*", int count = 100)
    {
        try
        {
            IDatabase db = EnsureConnected();
            ConnectionMultiplexer? connection = _connectionManager.GetConnection(DEFAULT_CONNECTION_NAME);
            int? currentDb = _connectionManager.GetCurrentDatabase(DEFAULT_CONNECTION_NAME);

            if (connection == null)
                throw new InvalidOperationException("Not connected to Redis");

            IServer server = connection.GetServer(connection.GetEndPoints().First());

            List<string> keys = server.Keys(database: currentDb ?? 0, pattern: pattern, pageSize: count)
                             .Take(count)
                             .Select(key => (string)key!)
                             .ToList();

            return JsonSerializer.Serialize(new
            {
                pattern,
                database = currentDb ?? 0,
                count = keys.Count,
                keys
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> GetKeyTypeAsync(string key)
    {
        try
        {
            IDatabase db = EnsureConnected();
            RedisType type = await db.KeyTypeAsync(key);
            
            return JsonSerializer.Serialize(new 
            {
                key,
                type = type.ToString().ToLower(),
                exists = type != RedisType.None
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> GetTtlAsync(string key)
    {
        try
        {
            IDatabase db = EnsureConnected();
            TimeSpan? ttl = await db.KeyTimeToLiveAsync(key);
            
            return JsonSerializer.Serialize(new 
            {
                key,
                ttl = ttl?.TotalSeconds,
                persistent = !ttl.HasValue,
                exists = await db.KeyExistsAsync(key)
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> SetExpireAsync(string key, TimeSpan expiry)
    {
        try
        {
            IDatabase db = EnsureConnected();
            bool result = await db.KeyExpireAsync(key, expiry);
            
            return JsonSerializer.Serialize(new 
            { 
                success = result,
                key,
                expiry = expiry.TotalSeconds,
                message = result ? "Expiry set successfully" : "Key not found or expiry failed"
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> GetInfoAsync(string section = "")
    {
        try
        {
            ConnectionMultiplexer? connection = _connectionManager.GetConnection(DEFAULT_CONNECTION_NAME);

            if (connection == null)
                throw new InvalidOperationException("Not connected to Redis");

            IServer server = connection.GetServer(connection.GetEndPoints().First());
            IGrouping<string, KeyValuePair<string, string>>[] info = await server.InfoAsync(section);

            var infoDict = new Dictionary<string, object>();
            foreach (IGrouping<string, KeyValuePair<string, string>> group in info)
            {
                var groupDict = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> item in group)
                {
                    groupDict[item.Key] = item.Value;
                }
                infoDict[group.Key] = groupDict;
            }

            return JsonSerializer.Serialize(new
            {
                section = string.IsNullOrEmpty(section) ? "all" : section,
                info = infoDict
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    public async Task<string> FlushDatabaseAsync()
    {
        try
        {
            ConnectionMultiplexer? connection = _connectionManager.GetConnection(DEFAULT_CONNECTION_NAME);
            int? currentDb = _connectionManager.GetCurrentDatabase(DEFAULT_CONNECTION_NAME);

            if (connection == null)
                throw new InvalidOperationException("Not connected to Redis");

            IServer server = connection.GetServer(connection.GetEndPoints().First());
            await server.FlushDatabaseAsync(currentDb ?? 0);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Database {currentDb ?? 0} flushed successfully"
            }, _options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _options);
        }
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "unknown";
        
        // Simple masking for passwords
        return Regex.Replace(
            connectionString, 
            @"password=([^,;]+)", 
            "password=***", 
            RegexOptions.IgnoreCase);
    }
}