namespace Mcp.Database.Core.Redis;

/// <summary>
/// Configuration options for Redis connections.
/// </summary>
public class RedisConnectionOptions
{
    /// <summary>
    /// Gets or sets the default connection name to use when not specified.
    /// </summary>
    public string DefaultConnectionName { get; set; } = "default";

    /// <summary>
    /// Gets or sets whether to enable automatic health checks.
    /// </summary>
    public bool HealthCheckEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between automatic health checks.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the timeout for connection operations.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to allow admin operations.
    /// </summary>
    public bool AllowAdmin { get; set; } = false;

    /// <summary>
    /// Gets or sets the default database number (0-15 for Redis).
    /// </summary>
    public int DefaultDatabase { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to automatically clean up unhealthy connections.
    /// </summary>
    public bool AutoCleanupUnhealthyConnections { get; set; } = true;
}

/// <summary>
/// Configuration for a single Redis connection profile.
/// </summary>
public class RedisConnectionProfile
{
    /// <summary>
    /// Gets or sets the connection name/identifier.
    /// </summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default database number for this connection (0-15).
    /// </summary>
    public int DefaultDatabase { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to automatically connect on startup.
    /// </summary>
    public bool AutoConnect { get; set; } = false;
}
