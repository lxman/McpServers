using AwsServer.Controllers.Models;

namespace AwsServer.Controllers.Responses;

public class SearchResponse
{
    public string LogGroupName { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public List<LogEvent> Events { get; set; } = [];
}