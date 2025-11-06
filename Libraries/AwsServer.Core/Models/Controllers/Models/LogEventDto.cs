namespace AwsServer.Core.Models.Controllers.Models;

public class LogEventDto
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string LogStreamName { get; set; } = string.Empty;
    public string? EventId { get; set; }
}