namespace AwsServer.Controllers.Requests;

public class FilterLogsRequest
{
    public required string LogGroupName { get; set; }
    public string? FilterPattern { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Limit { get; set; } = 100;
    public string? NextToken { get; set; }
}