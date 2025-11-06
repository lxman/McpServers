namespace AwsServer.Core.Services.CloudWatch.Models;

/// <summary>
/// Estimate of total event count
/// </summary>
public class EventCountEstimate
{
    public long EstimatedCount { get; set; }
    public bool IsExact { get; set; }
    public int SampleSize { get; set; }
    public required string Confidence { get; set; }
}