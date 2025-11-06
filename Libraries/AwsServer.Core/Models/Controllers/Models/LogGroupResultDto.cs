namespace AwsServer.Core.Models.Controllers.Models;

/// <summary>
/// Result from a single log group query
/// </summary>
public class LogGroupResultDto
{
    public required string LogGroupName { get; set; }
    public List<LogEventDto> Events { get; set; } = [];
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int QueryDurationMs { get; set; }
    public int EventCount { get; set; }
}