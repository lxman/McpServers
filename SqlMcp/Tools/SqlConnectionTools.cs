using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Mcp.Database.Core.Sql;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlServer.Core.Services;

namespace SqlMcp.Tools;

/// <summary>
/// Connection-management tools.
/// </summary>
/// <remarks>
/// These three took SqlServer.Core's IConnectionManager, whose only implementation was constructed
/// nowhere, so every call threw while resolving the tool type. Registering that implementation
/// would have been the wrong fix: it kept its own connection registry, read from
/// SqlConfiguration:Connections, while the other eleven tools resolve through SqlConnectionManager
/// -- so list_connections would have reported names execute_query could not use. They now share
/// the one registry everything else uses, with ConnectionResolver supplying the configured names.
/// </remarks>
[McpServerToolType]
public class SqlConnectionTools(
    SqlConnectionManager connectionManager,
    ConnectionResolver resolver,
    ILogger<SqlConnectionTools> logger)
{
    [McpServerTool, DisplayName("list_connections")]
    [Description("List available database connections. See connection-management/list_connections.md")]
    public string ListConnections()
    {
        try
        {
            var connections = resolver.ConfiguredNames
                .Select(name => new
                {
                    name,
                    connected = resolver.IsConnected(name)
                })
                .ToList();

            return JsonSerializer.Serialize(new { success = true, connections }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list connections");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("test_connection")]
    [Description("Test database connection. See connection-management/test_connection.md")]
    public async Task<string> TestConnection(
        string connectionName)
    {
        try
        {
            // Opening it is the test: a configured connection that has never been used is exactly
            // the case worth reporting on, and EnsureConnectedAsync explains why it could not open.
            await resolver.EnsureConnectedAsync(connectionName);
            bool isConnected = await connectionManager.PingConnectionAsync(connectionName);

            return JsonSerializer.Serialize(new { success = true, connectionName, isConnected }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connection test failed: {ConnectionName}", connectionName);
            return JsonSerializer.Serialize(new { success = false, connectionName, isConnected = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("close_connection")]
    [Description("Close database connection. See connection-management/close_connection.md")]
    public string CloseConnection(
        string connectionName)
    {
        try
        {
            bool closed = connectionManager.RemoveConnection(connectionName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                connectionName,
                message = closed
                    ? "Connection closed"
                    : "Connection was not open"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to close connection: {ConnectionName}", connectionName);
            return JsonSerializer.Serialize(new { success = false, connectionName, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
