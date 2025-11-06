namespace AzureServer.Core.Services.Monitor.Models;

/// <summary>
/// Metadata about metric query execution
/// </summary>
public class MetricQueryMetadata
{
    public DateTime QueryStartTime { get; set; }
    public DateTime QueryEndTime { get; set; }
    public long DurationMs { get; set; }
    public int TotalMetrics { get; set; }
    public int TotalDataPoints { get; set; }
    public TimeSpan? Interval { get; set; }
}