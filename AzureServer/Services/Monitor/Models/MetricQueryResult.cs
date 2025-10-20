namespace AzureServer.Services.Monitor.Models;

public class MetricQueryResult
{
    public string? Namespace { get; set; }
    public string? ResourceRegion { get; set; }
    public List<MetricData> Metrics { get; set; } = [];
    public string? Error { get; set; }
    
    /// <summary>
    /// Metadata about the metric query execution
    /// </summary>
    public MetricQueryMetadata? Metadata { get; set; }
}