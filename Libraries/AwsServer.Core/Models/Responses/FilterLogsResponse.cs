using AwsServer.Core.Common.Models;
using AwsServer.Core.Models.Controllers.Models;

namespace AwsServer.Core.Models.Responses;

public class FilterLogsResponse
{
    public List<LogEventDto> Events { get; set; } = [];
    public string? NextToken { get; set; }
    public bool HasMore { get; set; }
    public int SearchedLogStreams { get; set; }
    public int TotalEventsReturned { get; set; }
    public PaginationMetadata? Pagination { get; set; }
}
