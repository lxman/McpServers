using AwsServer.Core.Common.Models;
using AwsServer.Core.Models.Controllers.Models;

namespace AwsServer.Core.Models.Responses;

public class LogGroupsResponse
{
    public List<LogGroupDto> LogGroups { get; set; } = [];
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
    public PaginationMetadata? Pagination { get; set; }
}
