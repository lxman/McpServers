using AwsServer.Core.Models.Controllers.Models;

namespace AwsServer.Core.Models.Responses;

public class GetLogEventsResult
{
    public List<LogEventDto> Events { get; set; } = [];
    public string? NextForwardToken { get; set; }
    public string? NextBackwardToken { get; set; }
    public int TotalEventsReturned { get; set; }
}