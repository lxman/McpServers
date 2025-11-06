using AwsServer.Core.Models.Controllers.Models;

namespace AwsServer.Core.Models.Responses;

public class SearchResponse
{
    public string LogGroupName { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public List<LogEvent> Events { get; set; } = [];
}