namespace AzureServer.Services.Monitor.Models;

/// <summary>
/// Metadata about query execution
/// </summary>
public class QueryMetadata
{
    public DateTime QueryStartTime { get; set; }
    public DateTime QueryEndTime { get; set; }
    public long DurationMs { get; set; }
    public int TotalRows { get; set; }
    public int TotalTables { get; set; }
}