using System.Data.Common;
using Mcp.Database.Core.Sql;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServer.Core.Models;

namespace SqlServer.Core.Services;

/// <summary>
/// Opens the connections named in <see cref="SqlConfiguration"/> the first time one is asked for.
/// </summary>
/// <remarks>
/// This is the bridge that was missing. SqlConfiguration:Connections was read only by the old
/// ConnectionManager, which nothing ever constructed, while every live service resolves through
/// SqlConnectionManager -- which starts empty and has no tool that can populate it. The result was
/// that every connection name, including the ones spelled out in appsettings.json, answered
/// "not found. Please connect first." with no way to connect. Resolving lazily rather than seeding
/// at startup keeps backend start cheap and avoids dialling databases nobody asked for, which
/// matters because this server runs in the per-session pool and starts once per client.
/// </remarks>
public class ConnectionResolver(
    SqlConnectionManager connectionManager,
    IOptions<SqlConfiguration> config,
    ILogger<ConnectionResolver> logger)
{
    private readonly SqlConfiguration _config = config.Value;

    // AddConnectionAsync removes any existing entry before opening a new one, so two concurrent
    // resolutions of the same name could dispose a connection the other had just handed out. MCP
    // clients can and do issue tool calls in parallel, so the open is serialised.
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Names present in configuration, whether or not they are open yet.</summary>
    public IReadOnlyCollection<string> ConfiguredNames => _config.Connections.Keys;

    /// <summary>Whether the named connection is currently open.</summary>
    public bool IsConnected(string connectionName) =>
        connectionManager.GetConnection(connectionName) is not null;

    /// <summary>
    /// Returns an open connection for <paramref name="connectionName"/>, opening it from
    /// configuration if this is the first use.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The name is not configured, or it is configured but could not be opened.
    /// </exception>
    public async Task<DbConnection> EnsureConnectedAsync(string connectionName)
    {
        DbConnection? existing = connectionManager.GetConnection(connectionName);
        if (existing is not null) return existing;

        if (!_config.Connections.TryGetValue(connectionName, out ConnectionConfig? connConfig))
        {
            string known = _config.Connections.Count == 0
                ? "none are configured"
                : string.Join(", ", _config.Connections.Keys);
            throw new InvalidOperationException(
                $"Connection '{connectionName}' is not configured. Configured connections: {known}.");
        }

        await _gate.WaitAsync();
        try
        {
            // Another caller may have opened it while we waited.
            existing = connectionManager.GetConnection(connectionName);
            if (existing is not null) return existing;

            logger.LogInformation("Opening configured connection '{ConnectionName}' ({Provider}) on first use",
                connectionName, connConfig.Provider);

            // AddConnectionAsync reports failure by returning a message rather than throwing, so
            // the outcome is read back from the manager instead of from its return value.
            string outcome = await connectionManager.AddConnectionAsync(
                connectionName, connConfig.Provider, connConfig.ConnectionString);

            return connectionManager.GetConnection(connectionName)
                   ?? throw new InvalidOperationException(
                       $"Connection '{connectionName}' is configured but could not be opened: {outcome}");
        }
        finally
        {
            _gate.Release();
        }
    }
}
