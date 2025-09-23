namespace AwsMcp.CloudWatch.Models;

public class LogSearchMatch
{
    public string LogGroupName { get; set; } = string.Empty;
    public string LogStreamName { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public DateTime? IngestionTime { get; set; }
    public string EventId { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string MatchedLine { get; set; } = string.Empty;
    public List<LogContextLine> Context { get; set; } = new();
    public List<string> ExtractedValues { get; set; } = new();
}