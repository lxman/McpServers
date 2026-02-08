using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SshClient.Core.Models;
using SshClient.Core.Services;

namespace SshMcp.McpTools;

[McpServerToolType]
public sealed class SshConnectionTools(
    SshConnectionManager connectionManager,
    OutputGuard outputGuard,
    ILogger<SshConnectionTools> logger)
{
    [McpServerTool]
    [DisplayName("ssh_connect")]
    [Description("Connect to an SSH server. Returns connection info on success.")]
    public async Task<string> Connect(
        [Description("Unique name for this connection")] string connectionName,
        [Description("Hostname or IP address")] string host,
        [Description("SSH username")] string username,
        [Description("Path to private key file (optional if password provided)")] string? privateKeyPath = null,
        [Description("Passphrase for private key (optional)")] string? passphrase = null,
        [Description("Password (optional if key provided)")] string? password = null,
        [Description("SSH port (default: 22)")] int port = 22,
        [Description("Connection timeout in seconds (default: 30)")] int timeoutSeconds = 30)
    {
        try
        {
            var profile = new SshConnectionProfile
            {
                Name = connectionName,
                Host = host,
                Port = port,
                Username = username,
                PrivateKeyPath = privateKeyPath,
                Passphrase = passphrase,
                Password = password,
                TimeoutSeconds = timeoutSeconds
            };

            SshConnectionInfo result = await connectionManager.ConnectAsync(profile);
            return result.ToGuardedResponse(outputGuard, "ssh_connect");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to {Host}", host);
            return ex.ToErrorResponse(outputGuard, "Check hostname, credentials, and network connectivity");
        }
    }

    [McpServerTool]
    [DisplayName("ssh_connect_profile")]
    [Description("Connect using a saved SSH profile.")]
    public async Task<string> ConnectWithProfile(
        [Description("Name of the saved profile")] string profileName)
    {
        try
        {
            SshConnectionInfo result = await connectionManager.ConnectAsync(profileName);
            return result.ToGuardedResponse(outputGuard, "ssh_connect_profile");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect with profile {Profile}", profileName);
            return ex.ToErrorResponse(outputGuard, "Use ssh_list_profiles to see available profiles");
        }
    }

    [McpServerTool]
    [DisplayName("ssh_disconnect")]
    [Description("Disconnect an SSH connection.")]
    public string Disconnect(
        [Description("Name of the connection to disconnect")] string connectionName)
    {
        bool success = connectionManager.Disconnect(connectionName);
        return success
            ? new { Message = $"Disconnected: {connectionName}" }.ToSuccessResponse(outputGuard)
            : $"Connection '{connectionName}' not found".ToErrorResponse(outputGuard);
    }

    [McpServerTool]
    [DisplayName("ssh_disconnect_all")]
    [Description("Disconnect all SSH connections.")]
    public string DisconnectAll()
    {
        connectionManager.DisconnectAll();
        return new { Message = "All connections disconnected" }.ToSuccessResponse(outputGuard);
    }

    [McpServerTool]
    [DisplayName("ssh_list_connections")]
    [Description("List all active SSH connections.")]
    public string ListConnections()
    {
        IReadOnlyList<SshConnectionInfo> connections = connectionManager.GetConnections();
        return connections.ToGuardedResponse(outputGuard, "ssh_list_connections");
    }

    [McpServerTool]
    [DisplayName("ssh_get_connection")]
    [Description("Get details about a specific SSH connection.")]
    public string GetConnection(
        [Description("Name of the connection")] string connectionName)
    {
        SshConnectionInfo? connection = connectionManager.GetConnection(connectionName);
        return connection is not null
            ? connection.ToGuardedResponse(outputGuard, "ssh_get_connection")
            : $"Connection '{connectionName}' not found".ToErrorResponse(outputGuard);
    }

    [McpServerTool]
    [DisplayName("ssh_save_profile")]
    [Description("Save an SSH connection profile for reuse.")]
    public string SaveProfile(
        [Description("Unique name for this profile")] string profileName,
        [Description("Hostname or IP address")] string host,
        [Description("SSH username")] string username,
        [Description("Path to private key file (optional)")] string? privateKeyPath = null,
        [Description("Passphrase for private key (optional)")] string? passphrase = null,
        [Description("Password (optional)")] string? password = null,
        [Description("SSH port (default: 22)")] int port = 22,
        [Description("Connection timeout in seconds (default: 30)")] int timeoutSeconds = 30,
        [Description("Description of this profile")] string? description = null)
    {
        var profile = new SshConnectionProfile
        {
            Name = profileName,
            Host = host,
            Port = port,
            Username = username,
            PrivateKeyPath = privateKeyPath,
            Passphrase = passphrase,
            Password = password,
            TimeoutSeconds = timeoutSeconds,
            Description = description
        };

        connectionManager.SaveProfile(profile);
        return new { Message = $"Profile saved: {profileName}", Profile = profileName }.ToSuccessResponse(outputGuard);
    }

    [McpServerTool]
    [DisplayName("ssh_list_profiles")]
    [Description("List all saved SSH profiles.")]
    public string ListProfiles()
    {
        var profiles = connectionManager.GetProfiles()
            .Select(p => new
            {
                p.Name,
                p.Host,
                p.Port,
                p.Username,
                HasKey = !string.IsNullOrEmpty(p.PrivateKeyPath),
                HasPassword = !string.IsNullOrEmpty(p.Password),
                p.Description
            });

        return profiles.ToGuardedResponse(outputGuard, "ssh_list_profiles");
    }

    [McpServerTool]
    [DisplayName("ssh_remove_profile")]
    [Description("Remove a saved SSH profile.")]
    public string RemoveProfile(
        [Description("Name of the profile to remove")] string profileName)
    {
        bool success = connectionManager.RemoveProfile(profileName);
        return success
            ? new { Message = $"Profile removed: {profileName}" }.ToSuccessResponse(outputGuard)
            : $"Profile '{profileName}' not found".ToErrorResponse(outputGuard);
    }
}
