namespace AzureServer.Core.Services.Monitor.Models;

public class TimeSeriesData
{
    public Dictionary<string, string> MetadataValues { get; set; } = new();
    public List<MetricValue> Data { get; set; } = [];
}