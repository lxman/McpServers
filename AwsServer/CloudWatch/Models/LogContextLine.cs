namespace AwsServer.CloudWatch.Models;

public class LogContextLine
{
    public int LineNumber { get; set; }
    public DateTime? Timestamp { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsMatch { get; set; }
    public string LogStreamName { get; set; } = string.Empty;
}