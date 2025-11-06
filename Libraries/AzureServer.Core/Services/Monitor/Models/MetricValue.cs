namespace AzureServer.Core.Services.Monitor.Models;

public class MetricValue
{
    public DateTime TimeStamp { get; set; }
    public double? Average { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public double? Total { get; set; }
    public double? Count { get; set; }
}