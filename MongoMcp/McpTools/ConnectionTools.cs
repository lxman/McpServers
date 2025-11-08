using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MongoServer.Core;

namespace MongoMcp.McpTools;

/// <summary>
/// MCP tools for MongoDB server connection management
/// </summary>
[McpServerToolType]
public class ConnectionTools(
    ILogger<ConnectionTools> logger,
    MongoDbService mongoService)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("connect_to_server")]
    [Description("Connect to MongoDB server. See skills/mongo/connection/connect-to-server.md only when using this tool")]
    public async Task<string> ConnectToServer(string serverName, string connectionString, string databaseName)
    {
        try
        {
            logger.LogDebug("Connecting to MongoDB server: {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Connection string is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Database name is required" }, _jsonOptions);
            }

            var result = await mongoService.ConnectToServerAsync(serverName, connectionString, databaseName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                serverName,
                databaseName,
                message = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to MongoDB server: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("disconnect_from_server")]
    [Description("Disconnect from MongoDB server. See skills/mongo/connection/disconnect-from-server.md only when using this tool")]
    public string DisconnectFromServer(string serverName)
    {
        try
        {
            logger.LogDebug("Disconnecting from MongoDB server: {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            var result = mongoService.DisconnectFromServer(serverName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                serverName,
                message = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disconnecting from MongoDB server: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_active_connections")]
    [Description("List all active MongoDB connections. See skills/mongo/connection/list-active-connections.md only when using this tool")]
    public string ListActiveConnections()
    {
        try
        {
            logger.LogDebug("Listing active MongoDB connections");

            var result = mongoService.ListActiveConnections();

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing active connections");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("set_default_server")]
    [Description("Set default MongoDB server. See skills/mongo/connection/set-default-server.md only when using this tool")]
    public string SetDefaultServer(string serverName)
    {
        try
        {
            logger.LogDebug("Setting default MongoDB server: {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            var result = mongoService.SetDefaultServer(serverName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                serverName,
                message = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting default server: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_server_status")]
    [Description("Get MongoDB server connection status. See skills/mongo/connection/get-server-status.md only when using this tool")]
    public string GetServerStatus(string serverName)
    {
        try
        {
            logger.LogDebug("Getting MongoDB server status: {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            var result = mongoService.GetServerConnectionStatus(serverName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting server status: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("ping_server")]
    [Description("Ping MongoDB server to check connectivity. See skills/mongo/connection/ping-server.md only when using this tool")]
    public async Task<string> PingServer(string serverName)
    {
        try
        {
            logger.LogDebug("Pinging MongoDB server: {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            var result = await mongoService.PingServerAsync(serverName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pinging server: {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_connection_profiles")]
    [Description("List available MongoDB connection profiles. See skills/mongo/connection/list-connection-profiles.md only when using this tool")]
    public string ListConnectionProfiles()
    {
        try
        {
            logger.LogDebug("Listing MongoDB connection profiles");

            var result = mongoService.ListConnectionProfiles();

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing connection profiles");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("connect_with_profile")]
    [Description("Connect to MongoDB using a saved profile. See skills/mongo/connection/connect-with-profile.md only when using this tool")]
    public async Task<string> ConnectWithProfile(string profileName)
    {
        try
        {
            logger.LogDebug("Connecting to MongoDB using profile: {ProfileName}", profileName);

            if (string.IsNullOrWhiteSpace(profileName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Profile name is required" }, _jsonOptions);
            }

            var result = await mongoService.ConnectWithProfileAsync(profileName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                profileName,
                message = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting with profile: {ProfileName}", profileName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_autoconnect_status")]
    [Description("Get MongoDB auto-connect configuration status. See skills/mongo/connection/get-autoconnect-status.md only when using this tool")]
    public string GetAutoConnectStatus()
    {
        try
        {
            logger.LogDebug("Getting MongoDB auto-connect status");

            var result = mongoService.GetAutoConnectStatus();

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting auto-connect status");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
