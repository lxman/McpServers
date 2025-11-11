namespace Mcp.Database.Core.MongoDB;

/// <summary>
/// Configuration options for MongoDB connections.
/// </summary>
public class MongoConnectionOptions
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
    /// Gets or sets the maximum number of concurrent connections to maintain.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to automatically clean up unhealthy connections.
    /// </summary>
    public bool AutoCleanupUnhealthyConnections { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed operations.
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Configuration for a single MongoDB connection profile.
/// </summary>
public class MongoConnectionProfile
{
    /// <summary>
    /// Gets or sets the connection name/identifier.
    /// </summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default database name for this connection.
    /// </summary>
    public string DefaultDatabase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to automatically connect on startup.
    /// </summary>
    public bool AutoConnect { get; set; } = false;
}
