using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mcp.Common.Core;
using Mcp.Database.Core.Common;
using Mcp.Database.Core.Sql.Providers;

namespace Mcp.Database.Core.Sql;

/// <summary>
/// Manages SQL connections with support for multiple servers, providers, health monitoring, and connection pooling.
/// </summary>
public class SqlConnectionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, DbConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ISqlProvider> _providers = new();
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connectionInfo = new();

    private readonly Dictionary<string, ISqlProvider> _availableProviders;
    private readonly Timer? _healthCheckTimer;
    private readonly ILogger<SqlConnectionManager> _logger;
    private readonly SqlConnectionOptions _options;

    private string _defaultConnection = "default";
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlConnectionManager"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <param name="options">Configuration options for SQL connections</param>
    public SqlConnectionManager(ILogger<SqlConnectionManager> logger, SqlConnectionOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new SqlConnectionOptions();

        // Register available providers
        _availableProviders = new Dictionary<string, ISqlProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["SqlServer"] = new SqlServerProvider(),
            ["PostgreSQL"] = new PostgreSqlProvider(),
            ["MySQL"] = new MySqlProvider()
        };

        // Set up periodic health checks if enabled
        if (_options.HealthCheckEnabled)
        {
            _healthCheckTimer = new Timer(
                PerformHealthChecks,
                null,
                _options.HealthCheckInterval,
                _options.HealthCheckInterval);
        }

        _logger.LogInformation("SqlConnectionManager initialized with health checks {HealthChecks}",
            _options.HealthCheckEnabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Adds a new SQL connection.
    /// </summary>
    /// <param name="connectionName">Unique name for this connection</param>
    /// <param name="providerName">Provider name (SqlServer, PostgreSQL, MySQL)</param>
    /// <param name="connectionString">SQL connection string</param>
    /// <returns>Success message or error details</returns>
    public async Task<string> AddConnectionAsync(string connectionName, string providerName, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            throw new ArgumentException("Connection name cannot be empty", nameof(connectionName));

        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be empty", nameof(providerName));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (!_availableProviders.TryGetValue(providerName, out ISqlProvider? provider))
            throw new ArgumentException($"Unknown provider: {providerName}. Available providers: {string.Join(", ", _availableProviders.Keys)}", nameof(providerName));

        try
        {
            // Remove existing connection if it exists
            RemoveConnection(connectionName);

            DbConnection connection = provider.CreateConnection(connectionString);
            await connection.OpenAsync();

            // Test connection with ping
            DateTime startTime = DateTime.UtcNow;
            bool isHealthy = await provider.TestConnectionAsync(connection);
            TimeSpan pingDuration = DateTime.UtcNow - startTime;

            if (!isHealthy)
            {
                await connection.DisposeAsync();
                throw new InvalidOperationException("Connection test failed");
            }

            _connections[connectionName] = connection;
            _providers[connectionName] = provider;
            _connectionInfo[connectionName] = new ConnectionInfo
            {
                ConnectionName = connectionName,
                ConnectionString = connectionString,
                DatabaseName = connection.Database,
                DatabaseType = provider.ProviderName,
                ConnectedAt = DateTime.UtcNow,
                IsHealthy = true,
                LastPing = DateTime.UtcNow,
                LastPingDuration = pingDuration
            };

            _logger.LogInformation("Successfully connected to {Provider} server '{ConnectionName}' (database: {Database}, ping: {PingMs}ms)",
                provider.ProviderName, connectionName, connection.Database, pingDuration.TotalMilliseconds);

            return $"Successfully connected to {provider.ProviderName} server '{connectionName}' (database: {connection.Database})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Provider} server '{ConnectionName}'", providerName, connectionName);
            return $"Failed to connect to {providerName} server '{connectionName}': {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the database connection for a specific connection name.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>DbConnection instance, or null if not connected</returns>
    public DbConnection? GetConnection(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _connections.GetValueOrDefault(connectionName);
    }

    /// <summary>
    /// Gets the provider for a specific connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>ISqlProvider instance, or null if not connected</returns>
    public ISqlProvider? GetProvider(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;
        return _providers.GetValueOrDefault(connectionName);
    }

    /// <summary>
    /// Creates a new command for the specified connection.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>DbCommand instance, or null if not connected</returns>
    public DbCommand? CreateCommand(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        DbConnection? connection = GetConnection(connectionName);
        ISqlProvider? provider = GetProvider(connectionName);

        if (connection == null || provider == null)
            return null;

        return provider.CreateCommand(connection);
    }

    /// <summary>
    /// Removes a connection and disposes it.
    /// </summary>
    /// <param name="connectionName">Name of the connection to remove</param>
    /// <returns>True if removed successfully, false otherwise</returns>
    public bool RemoveConnection(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            return false;

        try
        {
            _connections.TryRemove(connectionName, out DbConnection? connection);
            _providers.TryRemove(connectionName, out _);
            _connectionInfo.TryRemove(connectionName, out _);

            if (connection != null)
            {
                connection.Dispose();
            }

            _logger.LogInformation("Disconnected from SQL connection '{ConnectionName}'", connectionName);
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

        if (!_connections.TryGetValue(connectionName, out DbConnection? connection))
            return false;

        if (!_connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info))
            return false;

        return connection.State == ConnectionState.Open && info.IsHealthy;
    }

    /// <summary>
    /// Pings a SQL connection to verify it's still alive.
    /// </summary>
    /// <param name="connectionName">Name of the connection (uses default if null)</param>
    /// <returns>True if ping successful, false otherwise</returns>
    public async Task<bool> PingConnectionAsync(string? connectionName = null)
    {
        connectionName = string.IsNullOrWhiteSpace(connectionName) ? _defaultConnection : connectionName;

        try
        {
            DbConnection? connection = GetConnection(connectionName);
            ISqlProvider? provider = GetProvider(connectionName);

            if (connection == null || provider == null)
                return false;

            DateTime startTime = DateTime.UtcNow;
            bool isHealthy = await provider.TestConnectionAsync(connection);
            TimeSpan pingDuration = DateTime.UtcNow - startTime;

            if (_connectionInfo.TryGetValue(connectionName, out ConnectionInfo? info))
            {
                info.UpdateHealth(isHealthy, pingDuration);
            }

            return isHealthy;
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
            info.DatabaseType,
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
    /// Gets a list of available SQL providers.
    /// </summary>
    /// <returns>List of provider names</returns>
    public List<string> GetAvailableProviders()
    {
        return _availableProviders.Keys.ToList();
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
    /// Disposes all SQL connections and stops health check timer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _healthCheckTimer?.Dispose();

        foreach (DbConnection connection in _connections.Values)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing DbConnection");
            }
        }

        _connections.Clear();
        _providers.Clear();
        _connectionInfo.Clear();

        _logger.LogInformation("SqlConnectionManager disposed");
    }
}
