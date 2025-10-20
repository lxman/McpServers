using Amazon.CloudWatchLogs.Model;
using AwsServer.Controllers.Models;

namespace AwsServer.Controllers.Responses;

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