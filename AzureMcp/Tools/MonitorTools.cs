using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.Monitor;
using AzureMcp.Services.Monitor.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class MonitorTools(IMonitorService monitorService)
{
    #region Log Analytics / CloudWatch Logs

    [McpServerTool]
    [Description("Query logs from a Log Analytics workspace using KQL (Kusto Query Language)")]
    public async Task<string> QueryLogsAsync(
        [Description("Log Analytics workspace ID")]
        string workspaceId,
        [Description("KQL query to execute")]
        string query,
        [Description("Optional time range in hours (default: 24)")]
        int? timeRangeHours = null)
    {
        try
        {
            TimeSpan? timeSpan = timeRangeHours.HasValue 
                ? TimeSpan.FromHours(timeRangeHours.Value) 
                : TimeSpan.FromHours(24);

            LogQueryResult result = await monitorService.QueryLogsAsync(workspaceId, query, timeSpan);
            
            if (result.Error is not null)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "QueryLogs");
        }
    }

    [McpServerTool]
    [Description("List all Log Analytics workspaces in a subscription")]
    public async Task<string> ListLogGroupsAsync(
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            IEnumerable<string> workspaces = await monitorService.ListLogGroupsAsync(subscriptionId);
            return JsonSerializer.Serialize(new { success = true, workspaces = workspaces.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListLogGroups");
        }
    }

    [McpServerTool]
    [Description("List all log streams (tables) in a Log Analytics workspace")]
    public async Task<string> ListLogStreamsAsync(
        [Description("Log Analytics workspace ID")]
        string workspaceId)
    {
        try
        {
            IEnumerable<string> streams = await monitorService.ListLogStreamsAsync(workspaceId);
            return JsonSerializer.Serialize(new { success = true, streams = streams.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListLogStreams");
        }
    }

    [McpServerTool]
    [Description("Search logs using regex patterns with context lines")]
    public async Task<string> SearchLogsWithRegexAsync(
        [Description("Log Analytics workspace ID")]
        string workspaceId,
        [Description("Regex pattern to search for (e.g., 'ERROR|Exception|timeout|failed' for errors)")]
        string regexPattern,
        [Description("Time range in hours (default: 24)")]
        int timeRangeHours = 24,
        [Description("Number of context lines around matches (default: 3)")]
        int contextLines = 3,
        [Description("Case sensitive search (default: false)")]
        bool caseSensitive = false,
        [Description("Maximum matches to return (default: 100)")]
        int maxMatches = 100)
    {
        try
        {
            List<LogMatch> matches = await monitorService.SearchLogsWithRegexAsync(
                workspaceId,
                regexPattern,
                TimeSpan.FromHours(timeRangeHours),
                contextLines,
                caseSensitive,
                maxMatches);

            return JsonSerializer.Serialize(new 
            { 
                success = true, 
                matchCount = matches.Count,
                matches 
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SearchLogsWithRegex");
        }
    }

    [McpServerTool]
    [Description("Search multiple Log Analytics workspaces using regex patterns")]
    public async Task<string> SearchMultipleWorkspacesWithRegexAsync(
        [Description("Comma-separated list of workspace IDs")]
        string workspaceIds,
        [Description("Regex pattern to search for")]
        string regexPattern,
        [Description("Time range in hours (default: 24)")]
        int timeRangeHours = 24,
        [Description("Number of context lines around matches (default: 2)")]
        int contextLines = 2,
        [Description("Case sensitive search (default: false)")]
        bool caseSensitive = false,
        [Description("Maximum matches to return across all workspaces (default: 100)")]
        int maxMatches = 100,
        [Description("Maximum workspaces to search (default: 5)")]
        int maxWorkspaces = 5)
    {
        try
        {
            IEnumerable<string> workspaceList = workspaceIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim());

            List<LogMatch> matches = await monitorService.SearchMultipleWorkspacesWithRegexAsync(
                workspaceList,
                regexPattern,
                TimeSpan.FromHours(timeRangeHours),
                contextLines,
                caseSensitive,
                maxMatches,
                maxWorkspaces);

            return JsonSerializer.Serialize(new 
            { 
                success = true, 
                matchCount = matches.Count,
                workspacesSearched = workspaceList.Take(maxWorkspaces).Count(),
                matches 
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SearchMultipleWorkspacesWithRegex");
        }
    }

    #endregion

    #region Metrics

    [McpServerTool]
    [Description("Query metrics for an Azure resource")]
    public async Task<string> QueryMetricsAsync(
        [Description("Resource ID (e.g., '/subscriptions/.../resourceGroups/.../providers/Microsoft.Web/sites/myapp')")]
        string resourceId,
        [Description("Comma-separated list of metric names")]
        string metricNames,
        [Description("Start time (ISO 8601 format, e.g., '2025-09-09T10:00:00Z')")]
        string startTime,
        [Description("End time (ISO 8601 format, e.g., '2025-09-09T11:00:00Z')")]
        string endTime,
        [Description("Optional interval in minutes (e.g., 5 for 5-minute intervals)")]
        int? intervalMinutes = null,
        [Description("Optional aggregations (comma-separated: Average,Sum,Maximum,Minimum,Count)")]
        string? aggregations = null)
    {
        try
        {
            DateTime start = DateTime.Parse(startTime);
            DateTime end = DateTime.Parse(endTime);
            IEnumerable<string> metrics = metricNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim());

            TimeSpan? interval = intervalMinutes.HasValue 
                ? TimeSpan.FromMinutes(intervalMinutes.Value) 
                : null;

            IEnumerable<string>? aggList = !string.IsNullOrEmpty(aggregations)
                ? aggregations.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim())
                : null;

            MetricQueryResult result = await monitorService.QueryMetricsAsync(
                resourceId, metrics, start, end, interval, aggList);

            if (result.Error is not null)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "QueryMetrics");
        }
    }

    [McpServerTool]
    [Description("List available metrics for an Azure resource")]
    public async Task<string> ListMetricsAsync(
        [Description("Resource ID")]
        string resourceId,
        [Description("Optional metric namespace to filter")]
        string? metricNamespace = null)
    {
        try
        {
            IEnumerable<string> metrics = await monitorService.ListMetricsAsync(resourceId, metricNamespace);
            return JsonSerializer.Serialize(new { success = true, metrics = metrics.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListMetrics");
        }
    }

    #endregion

    #region Application Insights

    [McpServerTool]
    [Description("List all Application Insights components")]
    public async Task<string> ListApplicationInsightsAsync(
        [Description("Optional subscription ID")]
        string? subscriptionId = null,
        [Description("Optional resource group name to filter")]
        string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ApplicationInsightsDto> components = await monitorService.ListApplicationInsightsAsync(
                subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new { success = true, components = components.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListApplicationInsights");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Application Insights component")]
    public async Task<string> GetApplicationInsightsAsync(
        [Description("Resource group name")]
        string resourceGroupName,
        [Description("Component name")]
        string componentName,
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            ApplicationInsightsDto? component = await monitorService.GetApplicationInsightsAsync(
                resourceGroupName, componentName, subscriptionId);

            if (component is null)
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = $"Application Insights component '{componentName}' not found in resource group '{resourceGroupName}'" 
                }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, component },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetApplicationInsights");
        }
    }

    #endregion

    #region Alerts

    [McpServerTool]
    [Description("List all alert rules")]
    public async Task<string> ListAlertsAsync(
        [Description("Optional subscription ID")]
        string? subscriptionId = null,
        [Description("Optional resource group name to filter")]
        string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<AlertRuleDto> alerts = await monitorService.ListAlertsAsync(
                subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new { success = true, alerts = alerts.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListAlerts");
        }
    }

    [McpServerTool]
    [Description("Create a new alert rule (log query alert)")]
    public async Task<string> CreateAlertAsync(
        [Description("Subscription ID")]
        string subscriptionId,
        [Description("Resource group name")]
        string resourceGroupName,
        [Description("Alert name")]
        string alertName,
        [Description("Alert description")]
        string description,
        [Description("Severity (0-4, where 0 is Critical)")]
        string severity,
        [Description("Log Analytics workspace ID")]
        string workspaceId,
        [Description("KQL query for the alert condition")]
        string query,
        [Description("Evaluation frequency (e.g., 'PT5M' for 5 minutes)")]
        string evaluationFrequency,
        [Description("Window size (e.g., 'PT15M' for 15 minutes)")]
        string windowSize)
    {
        try
        {
            AlertRuleDto? alert = await monitorService.CreateAlertAsync(
                subscriptionId, resourceGroupName, alertName, description, severity,
                workspaceId, query, evaluationFrequency, windowSize);

            if (alert is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Failed to create alert" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, alert },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateAlert");
        }
    }

    #endregion

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = ex.Message,
            operation,
            type = ex.GetType().Name,
            stackTrace = ex.StackTrace
        }, SerializerOptions.JsonOptionsIndented);
    }
}
