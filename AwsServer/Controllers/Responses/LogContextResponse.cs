using AwsServer.Controllers.Models;

namespace AwsServer.Controllers.Responses;

/// <summary>
/// Response containing log context around a specific timestamp
/// </summary>
public class LogContextResponse
{
    public long TargetTimestamp { get; set; }
    public LogEventDto? TargetEvent { get; set; }
    public int EventsBefore { get; set; }
    public int EventsAfter { get; set; }
    public List<LogEventDto> ContextEvents { get; set; } = [];
    public int TotalContextEvents { get; set; }
}