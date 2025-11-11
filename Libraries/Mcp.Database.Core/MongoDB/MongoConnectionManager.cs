using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Mcp.Common.Core;
using Mcp.Database.Core.Common;

namespace Mcp.Database.Core.MongoDB;

/// <summary>
/// Manages MongoDB connections with support for multiple servers, health monitoring, and connection pooling.
/// </summary>
public class MongoConnectionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, MongoClient> _clients = new();
    private readonly ConcurrentDictionary<string, IMongoDatabase> _databases = new();
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connectionInfo = new();
    private readonly ConcurrentDictionary<string, string> _currentDatabases = new();

    private readonly Timer? _healthCheckTimer;
    private readonly ILogger<MongoConnectionManager> _logger;
    private readonly MongoConnectionOptions _options;

    private string _defaultConnection = "default";
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoConnectionManager"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <param name="options">Configuration options for MongoDB connections</param>
    public MongoConnectionManager(ILogger<MongoConnectionManager> logger, MongoConnectionOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new MongoConnectionOptions();

        // Set up periodic health checks if enabled
        if (_options.HealthCheckEnabled)
        {
            _healthCheckTimer = new Timer(
                PerformHealthChecks,
                null,
                _options.HealthCheckInterval,
                _options.HealthCheckInterval);
        }

        _logger.LogInformation("MongoConnectionManager initialized with health checks {HealthChecks}",
            _options.HealthCheckEnabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Adds a new MongoDB connection.
    /// </summary>
    /// <param name="connectionName">Unique name for this connection</param>
    /// <param name="connectionString">MongoDB connection string</param>
    /// <param name="databaseName">Database name to use</param>
    /// <returns>Success message or error details</returns>
    public async Task<string> AddConnectionAsync(string connectionName, string connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            throw new ArgumentException("Connection name cannot be empty", nameof(connectionName));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        try
        {
            // Remove existing connection if it exists
            RemoveConnection(connectionName);

            var client = new MongoClient(connectionString);
            IMongoDatabase database = client.GetDatabase(databaseName);

            // Test connection with timeout
            DateTime startTime = DateTime.UtcNow;
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            TimeSpan pingDuration = DateTime.UtcNow - startTime;

            _clients[connectionName] = client;
            _databases[connectionName] = database;
            _currentDatabases[connectionName] = databaseName;
            _connectionInfo[connectionName] = new ConnectionInfo
            {
                ConnectionName = connectionName,
                ConnectionString = connectionString,
                DatabaseName = databaseName,
                DatabaseType = "MongoDB",
                ConnectedAt = DateTime.UtcNow,
                IsHealthy = true,
                LastPing = DateTime.UtcNow,
                LastPingDuration = pingDuration
            };

            _logger.LogInformation("Successfully connected to MongoDB server '{ConnectionName}' (database: '{DatabaseName}', ping: {PingMs}ms)",
                connectionName, databaseName, pingDuration.TotalMilliseconds);

            return $"Successfully connected to server '{connectionName}' (database: '{databaseName}')";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MongoDB server '{ConnectionName}'", connectionName);
            return $"Failed to connect to server '{connectionName}': {ex.Message}";
        }
    }

    /// <summary>
    /// Lists all databases on a MongoDB server.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>List of database names</returns>
    public async Task<List<string>> ListDatabasesAsync(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        try
        {
            MongoClient? client = GetClient(connectionName);
            if (client == null)
                throw new InvalidOperationException($"Connection '{connectionName}' is not connected");

            IAsyncCursor<string> databases = await client.ListDatabaseNamesAsync();
            return await databases.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list databases for connection '{ConnectionName}'", connectionName);
            throw;
        }
    }

    /// <summary>
    /// Switches to a different database on the same connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <param name="databaseName">Name of the database to switch to</param>
    /// <returns>Success message</returns>
    public async Task<string> SwitchDatabaseAsync(string? connectionName, string databaseName)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        try
        {
            MongoClient? client = GetClient(connectionName);
            if (client == null)
                throw new InvalidOperationException($"Connection '{connectionName}' is not connected");

            // Get the new database and test it
            IMongoDatabase newDatabase = client.GetDatabase(databaseName);
            await newDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

            // Update stored database and current database tracking
            _databases[connectionName] = newDatabase;
            _currentDatabases[connectionName] = databaseName;

            // Update connection info
            if (_connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info))
            {
                info.DatabaseName = databaseName;
            }

            _logger.LogInformation("Switched connection '{ConnectionName}' to database '{DatabaseName}'",
                connectionName, databaseName);

            return $"Successfully switched connection '{connectionName}' to database '{databaseName}'";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch database for connection '{ConnectionName}'", connectionName);
            throw new InvalidOperationException($"Failed to switch to database '{databaseName}' on connection '{connectionName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current database name for a connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>Current database name, or null if connection doesn't exist</returns>
    public string? GetCurrentDatabase(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _currentDatabases.GetValueOrDefault(connectionName);
    }

    /// <summary>
    /// Gets the MongoDB database instance for a connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>IMongoDatabase instance, or null if not connected</returns>
    public IMongoDatabase? GetDatabase(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _databases.GetValueOrDefault(connectionName);
    }

    /// <summary>
    /// Gets the MongoClient instance for a connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>MongoClient instance, or null if not connected</returns>
    public MongoClient? GetClient(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _clients.GetValueOrDefault(connectionName);
    }

    /// <summary>
    /// Removes a connection and disposes its client.
    /// </summary>
    /// <param name="connectionName">Name of the connection to remove</param>
    /// <returns>True if removed successfully, false otherwise</returns>
    public bool RemoveConnection(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            return false;

        try
        {
            _clients.TryRemove(connectionName, out MongoClient? client);
            _databases.TryRemove(connectionName, out _);
            _connectionInfo.TryRemove(connectionName, out _);
            _currentDatabases.TryRemove(connectionName, out _);

            client?.Dispose();

            _logger.LogInformation("Disconnected from MongoDB connection '{ConnectionName}'", connectionName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from connection '{ConnectionName}'", connectionName);
            return false;
        }
    }

    /// <summary>
    /// Checks if a connection is established and healthy.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>True if connected and healthy, false otherwise</returns>
    public bool IsConnected(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        return _databases.ContainsKey(connectionName) &&
               _connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info) &&
               info.IsHealthy;
    }

    /// <summary>
    /// Pings a MongoDB connection to verify it's still alive.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>True if ping successful, false otherwise</returns>
    public async Task<bool> PingConnectionAsync(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        try
        {
            IMongoDatabase? database = GetDatabase(connectionName);
            if (database == null)
                return false;

            DateTime startTime = DateTime.UtcNow;
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            TimeSpan pingDuration = DateTime.UtcNow - startTime;

            if (_connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info))
            {
                info.UpdateHealth(true, pingDuration);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ping failed for connection '{ConnectionName}'", connectionName);

            if (_connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info))
            {
                info.UpdateHealth(false);
            }

            return false;
        }
    }

    /// <summary>
    /// Sets the default connection to use when not specified.
    /// </summary>
    /// <param name="connectionName">Name of the connection to set as default</param>
    public void SetDefaultConnection(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            throw new ArgumentException("Connection name cannot be empty", nameof(connectionName));

        _defaultConnection = connectionName;
        _logger.LogInformation("Default connection set to '{ConnectionName}'", connectionName);
    }

    /// <summary>
    /// Gets the name of the default connection.
    /// </summary>
    /// <returns>Default connection name</returns>
    public string GetDefaultConnection() => _defaultConnection;

    /// <summary>
    /// Gets a list of all connection names.
    /// </summary>
    /// <returns>List of connection names</returns>
    public List<string> GetConnectionNames()
    {
        return _databases.Keys.ToList();
    }

    /// <summary>
    /// Gets connection information for a specific connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>ConnectionInfo instance, or null if not found</returns>
    public ConnectionInfo? GetConnectionInfo(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _connectionInfo.GetValueOrDefault(connectionName);
    }

    /// <summary>
    /// Gets a JSON summary of all connections and their status.
    /// </summary>
    /// <returns>JSON string with connection status</returns>
    public string GetConnectionsStatus()
    {
        var connections = _connectionInfo.Values.Select(info => new
        {
            info.ConnectionName,
            info.DatabaseName,
            CurrentDatabase = GetCurrentDatabase(info.ConnectionName),
            info.ConnectedAt,
            info.LastPing,
            info.IsHealthy,
            LastPingMs = info.LastPingDuration?.TotalMilliseconds,
            IsDefault = info.ConnectionName == _defaultConnection
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            defaultConnection = _defaultConnection,
            totalConnections = connections.Count,
            healthyConnections = connections.Count(c => c.IsHealthy),
            connections
        }, SerializerOptions.JsonOptionsIndented);
    }

    /// <summary>
    /// Performs health checks on all connections.
    /// </summary>
    private async void PerformHealthChecks(object? state)
    {
        if (_disposed)
            return;

        List<string> connectionNames = GetConnectionNames();
        foreach (string connectionName in connectionNames)
        {
            try
            {
                await PingConnectionAsync(connectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for connection '{ConnectionName}'", connectionName);
            }
        }

        // Auto cleanup unhealthy connections if enabled
        if (_options.AutoCleanupUnhealthyConnections)
        {
            CleanupUnhealthyConnections();
        }
    }

    /// <summary>
    /// Removes unhealthy connections from the connection pool.
    /// </summary>
    public void CleanupUnhealthyConnections()
    {
        List<string> connectionNames = GetConnectionNames().ToList();
        foreach (string connectionName in connectionNames)
        {
            if (!_connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info) || info.IsHealthy)
                continue;

            _logger.LogInformation("Cleaning up unhealthy connection '{ConnectionName}'", connectionName);
            RemoveConnection(connectionName);
        }
    }

    /// <summary>
    /// Disposes all MongoDB clients and stops health check timer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _healthCheckTimer?.Dispose();

        foreach (MongoClient client in _clients.Values)
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
        _currentDatabases.Clear();

        _logger.LogInformation("MongoConnectionManager disposed");
    }
}
