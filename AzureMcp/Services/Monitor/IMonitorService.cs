using AzureMcp.Services.Monitor.Models;

namespace AzureMcp.Services.Monitor;

public interface IMonitorService
{
    // Log Analytics queries
    Task<LogQueryResult> QueryLogsAsync(string workspaceId, string query, TimeSpan? timeSpan = null);
    Task<IEnumerable<string?>> ListLogGroupsAsync(string? subscriptionId = null);
    Task<IEnumerable<string>> ListLogStreamsAsync(string workspaceId);
    Task<List<LogMatch>> SearchLogsWithRegexAsync(string workspaceId, string regexPattern, 
        TimeSpan? timeSpan = null, int contextLines = 3, bool caseSensitive = false, int maxMatches = 100);
    Task<List<LogMatch>> SearchMultipleWorkspacesWithRegexAsync(IEnumerable<string> workspaceIds, 
        string regexPattern, TimeSpan? timeSpan = null, int contextLines = 2, bool caseSensitive = false, 
        int maxMatches = 100, int maxStreamsPerGroup = 5);
    
    // Metrics queries
    Task<MetricQueryResult> QueryMetricsAsync(string resourceId, IEnumerable<string> metricNames, 
        DateTime startTime, DateTime endTime, TimeSpan? interval = null, 
        IEnumerable<string>? aggregations = null);
    Task<IEnumerable<string>> ListMetricsAsync(string resourceId, string? metricNamespace = null);
    
    // Application Insights
    Task<IEnumerable<ApplicationInsightsDto>> ListApplicationInsightsAsync(string? subscriptionId = null, 
        string? resourceGroupName = null);
    Task<ApplicationInsightsDto?> GetApplicationInsightsAsync(string resourceGroupName, string componentName, 
        string? subscriptionId = null);
    
    // Alert Rules
    Task<IEnumerable<AlertRuleDto>> ListAlertsAsync(string? subscriptionId = null, 
        string? resourceGroupName = null);
    Task<AlertRuleDto?> CreateAlertAsync(string subscriptionId, string resourceGroupName, string alertName, 
        string description, string severity, string workspaceId, string query, string evaluationFrequency, 
        string windowSize);
}
