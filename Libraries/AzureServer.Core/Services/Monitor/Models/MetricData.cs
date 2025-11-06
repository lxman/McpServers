namespace AzureServer.Core.Services.Monitor.Models;

public class MetricData
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<TimeSeriesData> TimeSeries { get; set; } = [];
}