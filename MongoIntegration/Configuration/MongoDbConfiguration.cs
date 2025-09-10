namespace MongoIntegration.Configuration;

public class MongoDbConfiguration
{
    public string DefaultServer { get; set; } = "default";
    public string ConnectionString { get; set; } = string.Empty;
    public string DefaultDatabase { get; set; } = string.Empty;
    public bool AutoConnect { get; set; } = false;
    public List<ConnectionProfile> ConnectionProfiles { get; set; } = new();
    public ConnectionLimits ConnectionLimits { get; set; } = new();
    public Features Features { get; set; } = new();
}

public class ConnectionProfile
{
    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string DefaultDatabase { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool AutoConnect { get; set; } = false;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public class ConnectionLimits
{
    public int MaxConcurrentConnections { get; set; } = 5;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool HealthCheckEnabled { get; set; } = true;
    public bool AutoReconnectEnabled { get; set; } = true;
    public int RetryAttempts { get; set; } = 3;
}

public class Features
{
    public bool CrossServerOperationsEnabled { get; set; } = true;
    public bool HealthMonitoringEnabled { get; set; } = true;
    public bool AutoCleanupEnabled { get; set; } = true;
    public bool DetailedLoggingEnabled { get; set; } = false;
}
