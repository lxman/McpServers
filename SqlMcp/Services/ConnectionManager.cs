using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcp.Models;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Services;

public class ConnectionManager : IConnectionManager
{
    private readonly Dictionary<string, IDbConnection> _connections = new();
    private readonly Dictionary<string, IDbProvider> _providers = new();
    private readonly SqlConfiguration _config;
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(IOptions<SqlConfiguration> config, ILogger<ConnectionManager> logger)
    {
        _config = config.Value;
        _logger = logger;
        InitializeProviders();
    }

    private void InitializeProviders()
    {
        _providers["SqlServer"] = new SqlServerProvider();
        _providers["Sqlite"] = new SqliteProvider();
    }

    public async Task<IDbConnection> GetConnectionAsync(string connectionName)
    {
        if (_connections.TryGetValue(connectionName, out IDbConnection? existingConnection))
        {
            if (existingConnection.State == ConnectionState.Open)
                return existingConnection;

            existingConnection.Dispose();
            _connections.Remove(connectionName);
        }

        if (!_config.Connections.TryGetValue(connectionName, out ConnectionConfig? connConfig))
            throw new ArgumentException($"Connection '{connectionName}' not found in configuration");

        if (!_providers.TryGetValue(connConfig.Provider, out IDbProvider? provider))
            throw new NotSupportedException($"Provider '{connConfig.Provider}' not supported");

        IDbConnection connection = provider.CreateConnection(connConfig.ConnectionString);
        await Task.Run(() => connection.Open());
        _connections[connectionName] = connection;

        _logger.LogInformation("Opened connection: {ConnectionName}", connectionName);
        return connection;
    }

    public async Task<bool> TestConnectionAsync(string connectionName)
    {
        try
        {
            IDbConnection connection = await GetConnectionAsync(connectionName);
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed: {ConnectionName}", connectionName);
            return false;
        }
    }

    public IEnumerable<string> GetAvailableConnections()
    {
        return _config.Connections.Keys;
    }

    public async Task CloseConnectionAsync(string connectionName)
    {
        if (_connections.TryGetValue(connectionName, out IDbConnection? connection))
        {
            await Task.Run(() => connection.Close());
            connection.Dispose();
            _connections.Remove(connectionName);
            _logger.LogInformation("Closed connection: {ConnectionName}", connectionName);
        }
    }

    public async Task CloseAllConnectionsAsync()
    {
        foreach (KeyValuePair<string, IDbConnection> kvp in _connections.ToList())
        {
            await CloseConnectionAsync(kvp.Key);
        }
    }
}
