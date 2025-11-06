namespace SqlServer.Core.Models;

public class ConnectionConfig
{
    public required string Provider { get; set; }
    public required string ConnectionString { get; set; }
    public bool ReadOnly { get; set; }
}
