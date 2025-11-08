using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RedisBrowser.Core.Services;

public class RedisService
{
    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private string? _connectionString;
    private int _currentDatabase;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisService> _logger;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public RedisService(IConfiguration configuration, ILogger<RedisService> logger)
    {
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
            var envConnectionString = RegistryEnvironmentReader.GetEnvironmentVariableWithFallback("REDIS_CONNECTION_STRING");

            
            if (!string.IsNullOrEmpty(envConnectionString))
            {
                _logger.LogInformation("Attempting auto-connect using environment variables");
                await ConnectAsync(envConnectionString);
                return;
            }
            
            // Try the configuration file
            var configConnectionString = _configuration.GetConnectionString("Redis");
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
            _connectionString = connectionString;
            _connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            _database = _connection.GetDatabase(_currentDatabase);
            
            // Test the connection
            await _database.PingAsync();
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Successfully connected to Redis",
                database = _currentDatabase,
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
            _connection?.Dispose();
            _connection = null;
            _database = null;
            _connectionString = null;
            _currentDatabase = 0;
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = "Disconnected from Redis"
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
        if (_database == null || _connection == null)
        {
            return JsonSerializer.Serialize(new 
            { 
                connected = false,
                message = "Not connected to Redis"
            }, _options);
        }
        
        return JsonSerializer.Serialize(new 
        { 
            connected = true,
            database = _currentDatabase,
            connectionString = MaskConnectionString(_connectionString),
            isConnected = _connection.IsConnected
        }, _options);
    }

    private IDatabase EnsureConnected()
    {
        return _database ?? throw new InvalidOperationException("Not connected to Redis. Use redis-browser.connect command first.");
    }

    public async Task<string> SelectDatabaseAsync(int databaseNumber)
    {
        try
        {
            if (_connection == null)
                throw new InvalidOperationException("Not connected to Redis");

            _currentDatabase = databaseNumber;
            _database = _connection.GetDatabase(databaseNumber);
            
            // Test the database selection
            await _database.PingAsync();
            
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
            var db = EnsureConnected();
            var value = await db.StringGetAsync(key);
            
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
            var db = EnsureConnected();
            var result = await db.StringSetAsync(key, value, expiry);
            
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
            var db = EnsureConnected();
            var result = await db.KeyDeleteAsync(key);
            
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
            var db = EnsureConnected();
            var exists = await db.KeyExistsAsync(key);
            
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
            var db = EnsureConnected();
            var server = _connection!.GetServer(_connection.GetEndPoints().First());
            
            var keys = server.Keys(database: _currentDatabase, pattern: pattern, pageSize: count)
                             .Take(count)
                             .Select(key => (string)key!)
                             .ToList();
            
            return JsonSerializer.Serialize(new 
            {
                pattern,
                database = _currentDatabase,
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
            var db = EnsureConnected();
            var type = await db.KeyTypeAsync(key);
            
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
            var db = EnsureConnected();
            var ttl = await db.KeyTimeToLiveAsync(key);
            
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
            var db = EnsureConnected();
            var result = await db.KeyExpireAsync(key, expiry);
            
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
            var server = _connection!.GetServer(_connection.GetEndPoints().First());
            var info = await server.InfoAsync(section);
            
            var infoDict = new Dictionary<string, object>();
            foreach (var group in info)
            {
                var groupDict = new Dictionary<string, string>();
                foreach (var item in group)
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
            var server = _connection!.GetServer(_connection.GetEndPoints().First());
            await server.FlushDatabaseAsync(_currentDatabase);
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Database {_currentDatabase} flushed successfully"
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