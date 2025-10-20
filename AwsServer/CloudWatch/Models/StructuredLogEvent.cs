namespace AwsServer.CloudWatch.Models;

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