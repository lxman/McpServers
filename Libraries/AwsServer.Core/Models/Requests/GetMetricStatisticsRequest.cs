namespace AwsServer.Core.Models.Requests;

public class GetMetricStatisticsRequest
{
    public required string Namespace { get; set; }
    public required string MetricName { get; set; }
    public Dictionary<string, string>? Dimensions { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Period { get; set; } = 300;
    public List<string>? Statistics { get; set; }
}