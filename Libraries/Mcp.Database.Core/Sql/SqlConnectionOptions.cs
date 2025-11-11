namespace Mcp.Database.Core.Sql;

/// <summary>
/// Configuration options for SQL connections.
/// </summary>
public class SqlConnectionOptions
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
    /// Gets or sets whether to automatically clean up unhealthy connections.
    /// </summary>
    public bool AutoCleanupUnhealthyConnections { get; set; } = true;
}

/// <summary>
/// Configuration for a single SQL connection profile.
/// </summary>
public class SqlConnectionProfile
{
    /// <summary>
    /// Gets or sets the connection name/identifier.
    /// </summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider type ("SqlServer", "PostgreSQL", "MySQL", "Sqlite").
    /// </summary>
    public string Provider { get; set; } = "SqlServer";

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to automatically connect on startup.
    /// </summary>
    public bool AutoConnect { get; set; } = false;
}
