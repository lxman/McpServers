namespace SshClient.Core.Models;

/// <summary>
/// Represents a saved SSH connection profile
/// </summary>
public record SshConnectionProfile
{
    /// <summary>
    /// Unique name for this profile
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Hostname or IP address
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// SSH port (default 22)
    /// </summary>
    public int Port { get; init; } = 22;

    /// <summary>
    /// Username for authentication
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Path to private key file (optional if using password)
    /// </summary>
    public string? PrivateKeyPath { get; init; }

    /// <summary>
    /// Passphrase for private key (optional)
    /// </summary>
    public string? Passphrase { get; init; }

    /// <summary>
    /// Password for authentication (optional if using key)
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Optional description of this connection
    /// </summary>
    public string? Description { get; init; }
}
