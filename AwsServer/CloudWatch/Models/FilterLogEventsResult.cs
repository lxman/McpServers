using Amazon.CloudWatchLogs.Model;

namespace AwsServer.CloudWatch.Models;

/// <summary>
/// Paginated response for CloudWatch Logs filter events operation
/// </summary>
public class FilterLogEventsResult
{
    /// <summary>
    /// Log events in this page
    /// </summary>
    public List<Amazon.CloudWatchLogs.Model.FilteredLogEvent> Events { get; set; } = [];
    
    /// <summary>
    /// Number of events in this page
    /// </summary>
    public int EventCount { get; set; }
    
    /// <summary>
    /// Whether more results are available
    /// </summary>
    public bool HasMoreResults { get; set; }
    
    /// <summary>
    /// Token to retrieve next page of results. 
    /// Pass this to the next FilterLogEventsAsync call to continue pagination.
    /// </summary>
    public string? NextToken { get; set; }
    
    /// <summary>
    /// Number of log streams that were searched
    /// </summary>
    public List<SearchedLogStream> SearchedLogStreams { get; set; } = [];
    
    /// <summary>
    /// Human-readable summary of the results
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// Time range start (if specified)
    /// </summary>
    public DateTime? StartTime { get; set; }
    
    /// <summary>
    /// Time range end (if specified)
    /// </summary>
    public DateTime? EndTime { get; set; }
}