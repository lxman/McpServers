using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlMcp.Common;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Tools;

[McpServerToolType]
public class SqlConnectionTools(
    IConnectionManager connectionManager,
    ILogger<SqlConnectionTools> logger)
{
    [McpServerTool, DisplayName("list_connections")]
    [Description("List available database connections. See connection-management/list_connections.md")]
    public string ListConnections()
    {
        try
        {
            IEnumerable<string> connections = connectionManager.GetAvailableConnections();
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
        [Description("Connection name from configuration")] string connectionName)
    {
        try
        {
            bool isConnected = await connectionManager.TestConnectionAsync(connectionName);
            return JsonSerializer.Serialize(new { success = true, connectionName, isConnected }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connection test failed: {ConnectionName}", connectionName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("close_connection")]
    [Description("Close database connection. See connection-management/close_connection.md")]
    public async Task<string> CloseConnection(
        [Description("Connection name to close")] string connectionName)
    {
        try
        {
            await connectionManager.CloseConnectionAsync(connectionName);
            return JsonSerializer.Serialize(new { success = true, connectionName, message = "Connection closed" }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to close connection: {ConnectionName}", connectionName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
