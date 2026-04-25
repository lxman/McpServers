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
            })
            .ToList();

        if (profiles.Count == 0)
        {
            return new
            {
                Profiles = profiles,
                Message = "No saved SSH profiles exist.",
                ErrorCode = SshRecoveryCodes.NoMatchingProfile,
                Recoverable = true,
                Recovery = new SshRecoveryGuidance
                {
                    Message = "Create a connection with ssh_connect, then save it with ssh_save_profile if this target should be reused.",
                    Steps =
                    [
                        "If host, username, and authentication are known from context, call ssh_connect.",
                        "If required connection details are missing, ask the user for host, username, and authentication method.",
                        "After a successful ssh_connect, call ssh_save_profile when this target should be reused."
                    ],
                    Tools = ["ssh_connect", "ssh_save_profile"],
                    AskUserWhenMissing = ["host", "username", "privateKeyPath or password"]
                }
            }.ToGuardedResponse(outputGuard, "ssh_list_profiles");
        }

        return profiles.ToGuardedResponse(outputGuard, "ssh_list_profiles");
    }

    [McpServerTool]
    [DisplayName("ssh_connection_help")]
    [Description("Get recovery guidance for choosing or creating an SSH MCP connection. Use this when an SSH/SFTP operation fails due to a missing connection or missing profile.")]
    public string ConnectionHelp(
        [Description("Connection/profile name, host, username, or other hint from the failed operation")] string? connectionNameOrHint = null)
    {
        IReadOnlyList<SshConnectionInfo> connections = connectionManager.GetConnections();
        IReadOnlyList<SshConnectionProfile> profiles = connectionManager.GetProfiles();
        string? hint = string.IsNullOrWhiteSpace(connectionNameOrHint) ? null : connectionNameOrHint.Trim();

        var matchingConnections = connections
            .Where(c => MatchesHint(hint, c.Name, c.Host, c.Username))
            .Select(c => new
            {
                c.Name,
                c.Host,
                c.Port,
                c.Username,
                c.IsConnected,
                c.AuthMethod
            })
            .ToList();

        var matchingProfiles = profiles
            .Where(p => MatchesHint(hint, p.Name, p.Host, p.Username, p.Description))
            .Select(p => new
            {
                p.Name,
                p.Host,
                p.Port,
                p.Username,
                HasKey = !string.IsNullOrEmpty(p.PrivateKeyPath),
                HasPassword = !string.IsNullOrEmpty(p.Password),
                p.Description
            })
            .ToList();

        return new
        {
            Hint = hint,
            ActiveConnectionMatches = matchingConnections,
            SavedProfileMatches = matchingProfiles,
            ActiveConnectionCount = connections.Count,
            SavedProfileCount = profiles.Count,
            Recovery = new SshRecoveryGuidance
            {
                Message = "Use an existing MCP SSH connection or establish one before retrying the failed SSH/SFTP operation.",
                Steps =
                [
                    "If an active connection match exists, retry the original operation with that connection name.",
                    "If a saved profile match exists, call ssh_connect_profile with that profile name, then retry the original operation.",
                    "If no saved profile matches and host, username, and authentication are known from context, call ssh_connect.",
                    "If required connection details are missing, ask the user for host, username, and authentication method.",
                    "After a successful ssh_connect, call ssh_save_profile when this target should be reused."
                ],
                Tools = ["ssh_list_connections", "ssh_list_profiles", "ssh_connect_profile", "ssh_connect", "ssh_save_profile"],
                AskUserWhenMissing = ["host", "username", "privateKeyPath or password"]
            }
        }.ToGuardedResponse(outputGuard, "ssh_connection_help");
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

    private static bool MatchesHint(string? hint, params string?[] values)
    {
        if (hint is null)
            return true;

        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && (value.Contains(hint, StringComparison.OrdinalIgnoreCase)
                || hint.Contains(value, StringComparison.OrdinalIgnoreCase)));
    }
}
