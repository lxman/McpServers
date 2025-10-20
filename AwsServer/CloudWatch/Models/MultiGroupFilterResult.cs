using Amazon.CloudWatchLogs.Model;

namespace AwsServer.CloudWatch.Models;

/// <summary>
/// Result from filtering logs across multiple log groups
/// </summary>
public class MultiGroupFilterResult
{
    public List<LogGroupResult> LogGroupResults { get; set; } = [];
    public int TotalEvents { get; set; }
    public int TotalDurationMs { get; set; }
    public int SuccessfulQueries { get; set; }
    public int FailedQueries { get; set; }
}

/// <summary>
/// Result from a single log group query
/// </summary>
public class LogGroupResult
{
    public required string LogGroupName { get; set; }
    public List<FilteredLogEvent> Events { get; set; } = [];
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int QueryDurationMs { get; set; }
    public int EventCount { get; set; }
}
