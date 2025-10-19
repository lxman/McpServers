using AwsServer.Controllers.Models;

namespace AwsServer.Controllers.Responses;

public class LogGroupsResponse
{
    public List<LogGroupDto> LogGroups { get; set; } = [];
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
}