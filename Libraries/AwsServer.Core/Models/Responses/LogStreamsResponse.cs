using AwsServer.Core.Common.Models;
using AwsServer.Core.Models.Controllers.Models;

namespace AwsServer.Core.Models.Responses;

public class LogStreamsResponse
{
    public string LogGroupName { get; set; } = string.Empty;
    public List<LogStreamDto> Streams { get; set; } = [];
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
    public PaginationMetadata? Pagination { get; set; }
}
