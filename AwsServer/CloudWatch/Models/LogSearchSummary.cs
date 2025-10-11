namespace AwsServer.CloudWatch.Models;

public class LogSearchSummary
{
    public int TotalMatches { get; set; }
    public int LogGroupsSearched { get; set; }
    public int LogStreamsSearched { get; set; }
    public DateTime? FirstMatchTimestamp { get; set; }
    public DateTime? LastMatchTimestamp { get; set; }
    public Dictionary<string, int> ErrorPatterns { get; set; } = new();
    public Dictionary<string, int> LogStreamDistribution { get; set; } = new();
    public TimeSpan SearchDuration { get; set; }
}