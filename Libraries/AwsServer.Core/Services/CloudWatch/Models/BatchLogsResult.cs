using Amazon.CloudWatchLogs.Model;

namespace AwsServer.Core.Services.CloudWatch.Models;

/// <summary>
/// Result from batch retrieval of logs with automatic pagination
/// </summary>
public class BatchLogsResult
{
    public List<FilteredLogEvent> Events { get; set; } = [];
    public int TotalEvents { get; set; }
    public int PageCount { get; set; }
    public int TotalDurationMs { get; set; }
    public int AverageDurationPerPageMs { get; set; }
    public bool HasMoreResults { get; set; }
    public string? NextToken { get; set; }
}