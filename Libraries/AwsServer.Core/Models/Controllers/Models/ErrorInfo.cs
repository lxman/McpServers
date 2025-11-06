namespace AwsServer.Core.Models.Controllers.Models;

public class ErrorInfo
{
    public DateTime Timestamp { get; set; }
    public string LogStreamName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
}