using AwsServer.Common.Models;
using AwsServer.Controllers.Models;

namespace AwsServer.Controllers.Responses;

public class LogStreamsResponse
{
    public string LogGroupName { get; set; } = string.Empty;
    public List<LogStreamDto> Streams { get; set; } = [];
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
    public PaginationMetadata? Pagination { get; set; }
}
