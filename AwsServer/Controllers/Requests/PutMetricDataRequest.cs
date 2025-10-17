namespace AwsServer.Controllers.Requests;

public class PutMetricDataRequest
{
    public required string Namespace { get; set; }
    public required string MetricName { get; set; }
    public double Value { get; set; }
    public string? Unit { get; set; }
    public Dictionary<string, string>? Dimensions { get; set; }
}