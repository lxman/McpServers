using MongoDB.Bson;
using MongoDB.Driver;
using MongoIntegration.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MongoIntegration.Services;

public class ConnectionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, MongoClient> _clients = new();
    private readonly ConcurrentDictionary<string, IMongoDatabase> _databases = new();
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connectionInfo = new();
    
    // NEW: Track current database per server
    private readonly ConcurrentDictionary<string, string> _currentDatabases = new();
    
    private readonly Timer? _healthCheckTimer;
    private readonly ILogger<ConnectionManager> _logger;
    private string _defaultServer = "default";
    private bool _disposed = false;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
        
        // Set up periodic health checks every 5 minutes
        _healthCheckTimer = new Timer(PerformHealthChecks, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<string> AddConnectionAsync(string serverName, string connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            throw new ArgumentException("Server name cannot be empty", nameof(serverName));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        try
        {
            // Remove existing connection if it exists
            RemoveConnection(serverName);

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            
            // Test connection with timeout
            var startTime = DateTime.UtcNow;
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var pingDuration = DateTime.UtcNow - startTime;
            
            _clients[serverName] = client;
            _databases[serverName] = database;
            _currentDatabases[serverName] = databaseName; // NEW: Track current database
            _connectionInfo[serverName] = new ConnectionInfo
            {
                ServerName = serverName,
                ConnectionString = connectionString,
                DatabaseName = databaseName,
                ConnectedAt = DateTime.UtcNow,
                IsHealthy = true,
                LastPing = DateTime.UtcNow,
                LastPingDuration = pingDuration
            };
            
            _logger.LogInformation("Successfully connected to server '{ServerName}'", serverName);
            return $"Successfully connected to server '{serverName}' (database: '{databaseName}')";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server '{ServerName}'", serverName);
            return $"Failed to connect to server '{serverName}': {ex.Message}";
        }
    }

    // NEW: List all databases available on a server
    public async Task<List<string>> ListDatabasesAsync(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        try
        {
            var client = GetClient(serverName);
            if (client == null)
                throw new InvalidOperationException($"Server '{serverName}' is not connected");

            IAsyncCursor<string>? databases = await client.ListDatabaseNamesAsync();
            return await databases.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list databases for server '{ServerName}'", serverName);
            throw;
        }
    }

    // NEW: Switch to a different database on the same server
    public async Task<string> SwitchDatabaseAsync(string serverName, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        try
        {
            var client = GetClient(serverName);
            if (client == null)
                throw new InvalidOperationException($"Server '{serverName}' is not connected");

            // Get the new database and test it
            var newDatabase = client.GetDatabase(databaseName);
            await newDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

            // Update stored database and current database tracking
            _databases[serverName] = newDatabase;
            _currentDatabases[serverName] = databaseName;

            // Update connection info
            if (_connectionInfo.TryGetValue(serverName, out var info))
            {
                info.DatabaseName = databaseName;
            }

            _logger.LogInformation("Switched server '{ServerName}' to database '{DatabaseName}'", serverName, databaseName);
            return $"Successfully switched server '{serverName}' to database '{databaseName}'";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch database for server '{ServerName}'", serverName);
            throw new InvalidOperationException($"Failed to switch to database '{databaseName}' on server '{serverName}': {ex.Message}");
        }
    }

    // NEW: Get current database name for a server
    public string? GetCurrentDatabase(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        return _currentDatabases.TryGetValue(serverName, out var dbName) ? dbName : null;
    }

    // NEW: Get database by name (allows accessing any database on a connected server)
    public IMongoDatabase? GetDatabase(string serverName, string databaseName)
    {
        var client = GetClient(serverName);
        if (client == null)
            return null;

        return client.GetDatabase(databaseName);
    }

    public bool RemoveConnection(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return false;

        try
        {
            // Remove from all dictionaries
            _clients.TryRemove(serverName, out var client);
            _databases.TryRemove(serverName, out _);
            _connectionInfo.TryRemove(serverName, out _);
            _currentDatabases.TryRemove(serverName, out _); // NEW: Remove current database tracking

            // Dispose client if it exists
            client?.Dispose();

            _logger.LogInformation("Disconnected from server '{ServerName}'", serverName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from server '{ServerName}'", serverName);
            return false;
        }
    }

    public MongoClient? GetClient(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        return _clients.TryGetValue(serverName, out var client) ? client : null;
    }

    // MODIFIED: Original method still works for backward compatibility
    public IMongoDatabase? GetDatabase(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        return _databases.TryGetValue(serverName, out var database) ? database : null;
    }

    public bool IsConnected(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        return _databases.ContainsKey(serverName) && 
               _connectionInfo.TryGetValue(serverName, out var info) && 
               info.IsHealthy;
    }

    public async Task<bool> PingConnectionAsync(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        try
        {
            var database = GetDatabase(serverName);
            if (database == null)
                return false;

            var startTime = DateTime.UtcNow;
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var pingDuration = DateTime.UtcNow - startTime;

            if (_connectionInfo.TryGetValue(serverName, out var info))
            {
                info.UpdateHealth(true, pingDuration);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ping failed for server '{ServerName}'", serverName);
            
            if (_connectionInfo.TryGetValue(serverName, out var info))
            {
                info.UpdateHealth(false);
            }
            
            return false;
        }
    }

    public void SetDefaultServer(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            throw new ArgumentException("Server name cannot be empty", nameof(serverName));

        _defaultServer = serverName;
        _logger.LogInformation("Default server set to '{ServerName}'", serverName);
    }

    public string GetDefaultServer() => _defaultServer;

    public List<string> GetServerNames()
    {
        return _databases.Keys.ToList();
    }

    public ConnectionInfo? GetConnectionInfo(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            serverName = _defaultServer;

        return _connectionInfo.TryGetValue(serverName, out var info) ? info : null;
    }

    public string GetConnectionsStatus()
    {
        var connections = _connectionInfo.Values.Select(info => new
        {
            info.ServerName,
            info.DatabaseName,
            CurrentDatabase = GetCurrentDatabase(info.ServerName), // NEW: Show current database
            info.ConnectedAt,
            info.LastPing,
            info.IsHealthy,
            LastPingMs = info.LastPingDuration?.TotalMilliseconds,
            IsDefault = info.ServerName == _defaultServer
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            defaultServer = _defaultServer,
            totalConnections = connections.Count,
            healthyConnections = connections.Count(c => c.IsHealthy),
            connections
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private async void PerformHealthChecks(object? state)
    {
        if (_disposed)
            return;

        var serverNames = GetServerNames();
        foreach (var serverName in serverNames)
        {
            try
            {
                await PingConnectionAsync(serverName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for server '{ServerName}'", serverName);
            }
        }
    }

    public void CleanupConnections()
    {
        var serverNames = GetServerNames().ToList();
        foreach (var serverName in serverNames)
        {
            if (_connectionInfo.TryGetValue(serverName, out var info) && !info.IsHealthy)
            {
                _logger.LogInformation("Cleaning up unhealthy connection to '{ServerName}'", serverName);
                RemoveConnection(serverName);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        _healthCheckTimer?.Dispose();
        
        foreach (var client in _clients.Values)
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MongoClient");
            }
        }
        
        _clients.Clear();
        _databases.Clear();
        _connectionInfo.Clear();
        _currentDatabases.Clear(); // NEW: Clear current database tracking
    }
}
