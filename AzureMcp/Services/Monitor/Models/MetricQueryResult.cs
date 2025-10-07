namespace AzureMcp.Services.Monitor.Models;

public class MetricQueryResult
{
    public string? Namespace { get; set; }
    public string? ResourceRegion { get; set; }
    public List<MetricData> Metrics { get; set; } = [];
    public string? Error { get; set; }
}

public class MetricData
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<TimeSeriesData> TimeSeries { get; set; } = [];
}

public class TimeSeriesData
{
    public Dictionary<string, string> MetadataValues { get; set; } = new();
    public List<MetricValue> Data { get; set; } = [];
}

public class MetricValue
{
    public DateTime TimeStamp { get; set; }
    public double? Average { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public double? Total { get; set; }
    public double? Count { get; set; }
}
