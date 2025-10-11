using System.Text.Json;
using StackExchange.Redis;

namespace RedisBrowser.Services;

public class RedisService
{
    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private string? _connectionString;
    private int _currentDatabase = 0;
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
            // Try environment variables first
            string? envConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
            
            if (!string.IsNullOrEmpty(envConnectionString))
            {
                _logger.LogInformation("Attempting auto-connect using environment variables");
                await ConnectAsync(envConnectionString);
                return;
            }
            
            // Try configuration file
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
            _connectionString = connectionString;
            _connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            _database = _connection.GetDatabase(_currentDatabase);
            
            // Test the connection
            await _database.PingAsync();
            
            return JsonSerializer.Serialize(new 
            { 
                success = true,
                message = $"Successfully connected to Redis",
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
        if (_database == null)
            throw new InvalidOperationException("Not connected to Redis. Use redis-browser.connect command first.");
        return _database;
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
            IDatabase db = EnsureConnected();
            RedisValue value = await db.StringGetAsync(key);
            
            return JsonSerializer.Serialize(new 
            { 
                key = key,
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
            bool result = await db.StringSetAsync(key, value, expiry);
            
            return JsonSerializer.Serialize(new 
            { 
                success = result,
                key = key,
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
                key = key,
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
                key = key,
                exists = exists
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
            IServer server = _connection!.GetServer(_connection.GetEndPoints().First());
            
            List<string> keys = server.Keys(database: _currentDatabase, pattern: pattern, pageSize: count)
                             .Take(count)
                             .Select(key => (string)key!)
                             .ToList();
            
            return JsonSerializer.Serialize(new 
            { 
                pattern = pattern,
                database = _currentDatabase,
                count = keys.Count,
                keys = keys
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
                key = key,
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
                key = key,
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
                key = key,
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
            IServer server = _connection!.GetServer(_connection.GetEndPoints().First());
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
            IServer server = _connection!.GetServer(_connection.GetEndPoints().First());
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
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString, 
            @"password=([^,;]+)", 
            "password=***", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
