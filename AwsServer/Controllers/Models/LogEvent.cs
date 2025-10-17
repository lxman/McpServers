namespace AwsServer.Controllers.Models;

public class LogEvent
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string LogStreamName { get; set; } = string.Empty;
}