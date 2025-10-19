using Amazon.CloudWatchLogs.Model;
using QueryStatistics = AwsServer.Controllers.Models.QueryStatistics;

namespace AwsServer.Controllers.Responses;

public class InsightsQueryResult
{
    public string? QueryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<List<ResultField>>? Results { get; set; }
    public QueryStatistics? Statistics { get; set; }
}