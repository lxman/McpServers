using AwsServer.Controllers.Models;

namespace AwsServer.Controllers.Responses;

public class GetLogEventsResult
{
    public List<LogEventDto> Events { get; set; } = [];
    public string? NextForwardToken { get; set; }
    public string? NextBackwardToken { get; set; }
    public int TotalEventsReturned { get; set; }
}