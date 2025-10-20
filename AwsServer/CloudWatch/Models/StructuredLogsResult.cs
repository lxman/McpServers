namespace AwsServer.CloudWatch.Models;

/// <summary>
/// Result from structured log parsing
/// </summary>
public class StructuredLogsResult
{
    public List<StructuredLogEvent> Events { get; set; } = [];
    public int TotalEvents { get; set; }
    public Dictionary<string, int> FormatStatistics { get; set; } = new();
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
}