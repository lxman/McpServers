using Amazon.CloudWatchLogs.Model;

namespace AwsServer.Core.Services.CloudWatch.Models;

/// <summary>
/// Result containing log context around a specific timestamp
/// </summary>
public class LogContextResult
{
    public long TargetTimestamp { get; set; }
    public OutputLogEvent? TargetEvent { get; set; }
    public int EventsBefore { get; set; }
    public int EventsAfter { get; set; }
    public List<OutputLogEvent> ContextEvents { get; set; } = [];
    public int TotalContextEvents { get; set; }
}
