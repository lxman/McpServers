namespace SshClient.Core.Models;

/// <summary>
/// Information about an active SSH connection
/// </summary>
public record SshConnectionInfo
{
    /// <summary>
    /// Profile name for this connection
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Remote hostname
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// SSH port
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Username used for connection
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Whether the connection is currently active
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// When the connection was established
    /// </summary>
    public DateTime? ConnectedAt { get; init; }

    /// <summary>
    /// Authentication method used (key or password)
    /// </summary>
    public string AuthMethod { get; init; } = "unknown";

    /// <summary>
    /// Server SSH version string
    /// </summary>
    public string? ServerVersion { get; init; }

    /// <summary>
    /// Last error message if connection failed
    /// </summary>
    public string? LastError { get; init; }
}
