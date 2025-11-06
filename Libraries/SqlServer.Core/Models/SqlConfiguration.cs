namespace SqlServer.Core.Models;

public class SqlConfiguration
{
    public Dictionary<string, ConnectionConfig> Connections { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
}

public class SecuritySettings
{
    public bool AllowDdl { get; set; }
    public int MaxResultRows { get; set; } = 10000;
    public bool AuditAllQueries { get; set; } = true;
}
