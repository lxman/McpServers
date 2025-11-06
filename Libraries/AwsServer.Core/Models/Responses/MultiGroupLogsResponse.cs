using AwsServer.Core.Models.Controllers.Models;

namespace AwsServer.Core.Models.Responses;

/// <summary>
/// Response from filtering logs across multiple log groups
/// </summary>
public class MultiGroupLogsResponse
{
    public List<LogGroupResultDto> LogGroupResults { get; set; } = [];
    public int TotalEvents { get; set; }
    public int TotalDurationMs { get; set; }
    public int SuccessfulQueries { get; set; }
    public int FailedQueries { get; set; }
}