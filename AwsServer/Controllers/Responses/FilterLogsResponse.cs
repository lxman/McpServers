using AwsServer.Controllers.Models;

namespace AwsServer.Controllers.Responses;

public class FilterLogsResponse
{
    public List<LogEventDto> Events { get; set; } = [];
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
    public int SearchedLogStreams { get; set; }
    public int TotalEventsReturned { get; set; }
}