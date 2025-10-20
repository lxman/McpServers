using Amazon.CloudWatchLogs.Model;

namespace AwsServer.CloudWatch.Models;

/// <summary>
/// Result from batch retrieval of logs with automatic pagination
/// </summary>
public class BatchLogsResult
{
    public List<FilteredLogEvent> Events { get; set; } = new();
    public int TotalEvents { get; set; }
    public int PageCount { get; set; }
    public int TotalDurationMs { get; set; }
    public int AverageDurationPerPageMs { get; set; }
    public bool HasMoreResults { get; set; }
    public string? NextToken { get; set; }
}

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

/// <summary>
/// Result from structured log parsing
/// </summary>
public class StructuredLogsResult
{
    public List<StructuredLogEvent> Events { get; set; } = new();
    public int TotalEvents { get; set; }
    public Dictionary<string, int> FormatStatistics { get; set; } = new();
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// A single structured log event with parsed data
/// </summary>
public class StructuredLogEvent
{
    public DateTime Timestamp { get; set; }
    public string? LogStreamName { get; set; }
    public string? EventId { get; set; }
    public required string RawMessage { get; set; }
    public required string Format { get; set; }  // "json", "key-value", "plain-text", "unknown"
    public Dictionary<string, object?>? ParsedData { get; set; }
}
