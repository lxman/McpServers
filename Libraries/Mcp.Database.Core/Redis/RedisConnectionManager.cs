using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Mcp.Common.Core;
using Mcp.Database.Core.Common;

namespace Mcp.Database.Core.Redis;

/// <summary>
/// Manages Redis connections with support for multiple servers, health monitoring, and connection pooling.
/// </summary>
public class RedisConnectionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ConnectionMultiplexer> _connections = new();
    private readonly ConcurrentDictionary<string, int> _currentDatabases = new();
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connectionInfo = new();

    private readonly Timer? _healthCheckTimer;
    private readonly ILogger<RedisConnectionManager> _logger;
    private readonly RedisConnectionOptions _options;

    private string _defaultConnection = "default";
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisConnectionManager"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <param name="options">Configuration options for Redis connections</param>
    public RedisConnectionManager(ILogger<RedisConnectionManager> logger, RedisConnectionOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new RedisConnectionOptions();

        // Set up periodic health checks if enabled
        if (_options.HealthCheckEnabled)
        {
            _healthCheckTimer = new Timer(
                PerformHealthChecks,
                null,
                _options.HealthCheckInterval,
                _options.HealthCheckInterval);
        }

        _logger.LogInformation("RedisConnectionManager initialized with health checks {HealthChecks}",
            _options.HealthCheckEnabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Adds a new Redis connection.
    /// </summary>
    /// <param name="connectionName">Unique name for this connection</param>
    /// <param name="connectionString">Redis connection string</param>
    /// <param name="database">Database number to use (0-15, default: 0)</param>
    /// <returns>Success message or error details</returns>
    public async Task<string> AddConnectionAsync(string connectionName, string connectionString, int database = 0)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            throw new ArgumentException("Connection name cannot be empty", nameof(connectionName));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (database < 0 || database > 15)
            throw new ArgumentException("Database must be between 0 and 15", nameof(database));

        try
        {
            // Remove existing connection if it exists
            RemoveConnection(connectionName);

            ConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            IDatabase db = connection.GetDatabase(database);

            // Test connection with ping
            DateTime startTime = DateTime.UtcNow;
            await db.PingAsync();
            TimeSpan pingDuration = DateTime.UtcNow - startTime;

            _connections[connectionName] = connection;
            _currentDatabases[connectionName] = database;
            _connectionInfo[connectionName] = new ConnectionInfo
            {
                ConnectionName = connectionName,
                ConnectionString = connectionString,
                DatabaseName = $"Database {database}",
                DatabaseType = "Redis",
                ConnectedAt = DateTime.UtcNow,
                IsHealthy = true,
                LastPing = DateTime.UtcNow,
                LastPingDuration = pingDuration
            };

            _logger.LogInformation("Successfully connected to Redis server '{ConnectionName}' (database: {Database}, ping: {PingMs}ms)",
                connectionName, database, pingDuration.TotalMilliseconds);

            return $"Successfully connected to server '{connectionName}' (database: {database})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis server '{ConnectionName}'", connectionName);
            return $"Failed to connect to server '{connectionName}': {ex.Message}";
        }
    }

    /// <summary>
    /// Switches to a different database on the same connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <param name="database">Database number to switch to (0-15)</param>
    /// <returns>Success message</returns>
    public async Task<string> SelectDatabaseAsync(string? connectionName, int database)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        if (database < 0 || database > 15)
            throw new ArgumentException("Database must be between 0 and 15", nameof(database));

        try
        {
            ConnectionMultiplexer? connection = GetConnection(connectionName);
            if (connection == null)
                throw new InvalidOperationException($"Connection '{connectionName}' is not connected");

            // Test the new database
            IDatabase db = connection.GetDatabase(database);
            await db.PingAsync();

            // Update current database tracking
            _currentDatabases[connectionName] = database;

            // Update connection info
            if (_connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info))
            {
                info.DatabaseName = $"Database {database}";
            }

            _logger.LogInformation("Switched connection '{ConnectionName}' to database {Database}",
                connectionName, database);

            return $"Successfully switched connection '{connectionName}' to database {database}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch database for connection '{ConnectionName}'", connectionName);
            throw new InvalidOperationException($"Failed to switch to database {database} on connection '{connectionName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current database number for a connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>Current database number, or null if connection doesn't exist</returns>
    public int? GetCurrentDatabase(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _currentDatabases.GetValueOrDefault(connectionName, -1) == -1 ? null : _currentDatabases[connectionName];
    }

    /// <summary>
    /// Gets the Redis database instance for a connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <param name="database">Optional database number (uses current database if null)</param>
    /// <returns>IDatabase instance, or null if not connected</returns>
    public IDatabase? GetDatabase(string? connectionName = null, int? database = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        ConnectionMultiplexer? connection = GetConnection(connectionName);
        if (connection == null)
            return null;

        int dbNumber = database ?? GetCurrentDatabase(connectionName) ?? 0;
        return connection.GetDatabase(dbNumber);
    }

    /// <summary>
    /// Gets the ConnectionMultiplexer instance for a connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>ConnectionMultiplexer instance, or null if not connected</returns>
    public ConnectionMultiplexer? GetConnection(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _connections.GetValueOrDefault(connectionName);
    }

    /// <summary>
    /// Removes a connection and disposes its ConnectionMultiplexer.
    /// </summary>
    /// <param name="connectionName">Name of the connection to remove</param>
    /// <returns>True if removed successfully, false otherwise</returns>
    public bool RemoveConnection(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            return false;

        try
        {
            _connections.TryRemove(connectionName, out ConnectionMultiplexer? connection);
            _currentDatabases.TryRemove(connectionName, out _);
            _connectionInfo.TryRemove(connectionName, out _);

            connection?.Dispose();

            _logger.LogInformation("Disconnected from Redis connection '{ConnectionName}'", connectionName);
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

        return _connections.TryGetValue(connectionName, out ConnectionMultiplexer? connection) &&
               connection.IsConnected &&
               _connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info) &&
               info.IsHealthy;
    }

    /// <summary>
    /// Pings a Redis connection to verify it's still alive.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>True if ping successful, false otherwise</returns>
    public async Task<bool> PingConnectionAsync(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        try
        {
            IDatabase? database = GetDatabase(connectionName);
            if (database == null)
                return false;

            DateTime startTime = DateTime.UtcNow;
            await database.PingAsync();
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
        return _connections.Keys.ToList();
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
    /// Disposes all Redis connections and stops health check timer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _healthCheckTimer?.Dispose();

        foreach (ConnectionMultiplexer connection in _connections.Values)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing ConnectionMultiplexer");
            }
        }

        _connections.Clear();
        _currentDatabases.Clear();
        _connectionInfo.Clear();

        _logger.LogInformation("RedisConnectionManager disposed");
    }
}
