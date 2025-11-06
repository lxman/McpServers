using Amazon.CloudWatchLogs.Model;
using QueryStatistics = AwsServer.Core.Models.Controllers.Models.QueryStatistics;

namespace AwsServer.Core.Models.Responses;

public class InsightsQueryResult
{
    public string? QueryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<List<ResultField>>? Results { get; set; }
    public QueryStatistics? Statistics { get; set; }
}