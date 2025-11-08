using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.Monitor;
using AzureServer.Core.Services.Monitor.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Monitor operations
/// </summary>
[McpServerToolType]
public class MonitorTools(
    IMonitorService monitorService,
    ILogger<MonitorTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("query_logs")]
    [Description("Query logs from Azure Monitor workspace. See skills/azure/monitor/query-logs.md only when using this tool")]
    public async Task<string> QueryLogs(
        string workspaceId,
        string query,
        int? timeRangeHours = null,
        bool useQuickEstimate = true,
        int? maxResults = 1000)
    {
        try
        {
            logger.LogDebug("Querying logs from workspace {WorkspaceId}", workspaceId);

            var timeSpan = timeRangeHours.HasValue
                ? TimeSpan.FromHours(timeRangeHours.Value)
                : TimeSpan.FromHours(24);

            var limitedQuery = $"{query} | take {maxResults ?? 1000}";

            var result = await monitorService.QueryLogsAsync(workspaceId, limitedQuery, timeSpan);

            if (result.Error is not null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.Error
                }, _jsonOptions);
            }

            var estimate = await monitorService.GetLogCountEstimateAsync(
                workspaceId,
                query,
                timeSpan,
                useQuickEstimate);

            var totalRows = result.Tables.Sum(t => t.Rows.Count);
            result.Pagination = monitorService.CalculatePaginationMetadata(
                totalRows,
                maxResults ?? 1000,
                1,
                false,
                estimate.count,
                estimate.confidence);

            return JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying logs from workspace {WorkspaceId}", workspaceId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "QueryLogs",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_log_workspaces")]
    [Description("List Azure Monitor Log Analytics workspaces. See skills/azure/monitor/list-log-workspaces.md only when using this tool")]
    public async Task<string> ListLogWorkspaces(string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing log workspaces");
            var workspaces = await monitorService.ListLogGroupsAsync(subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                workspaces = workspaces.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log workspaces");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListLogWorkspaces",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_log_streams")]
    [Description("List log streams in a workspace. See skills/azure/monitor/list-log-streams.md only when using this tool")]
    public async Task<string> ListLogStreams(string workspaceId)
    {
        try
        {
            logger.LogDebug("Listing log streams for workspace {WorkspaceId}", workspaceId);
            var streams = await monitorService.ListLogStreamsAsync(workspaceId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                streams = streams.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log streams for workspace {WorkspaceId}", workspaceId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListLogStreams",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("search_logs_regex")]
    [Description("Search logs using regex pattern. See skills/azure/monitor/search-logs-regex.md only when using this tool")]
    public async Task<string> SearchLogsWithRegex(
        string workspaceId,
        string regexPattern,
        int timeRangeHours = 24,
        int contextLines = 3,
        bool caseSensitive = false,
        int maxMatches = 100)
    {
        try
        {
            logger.LogDebug("Searching logs with regex in workspace {WorkspaceId}", workspaceId);

            var matches = await monitorService.SearchLogsWithRegexAsync(
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
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching logs with regex");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "SearchLogsWithRegex",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("search_multiple_workspaces_regex")]
    [Description("Search multiple workspaces using regex. See skills/azure/monitor/search-multiple-workspaces-regex.md only when using this tool")]
    public async Task<string> SearchMultipleWorkspacesWithRegex(
        string workspaceIds,
        string regexPattern,
        int timeRangeHours = 24,
        int contextLines = 2,
        bool caseSensitive = false,
        int maxMatches = 100,
        int maxWorkspaces = 5)
    {
        try
        {
            logger.LogDebug("Searching multiple workspaces with regex");

            IEnumerable<string> workspaceList = workspaceIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim()).ToList();

            var matches = await monitorService.SearchMultipleWorkspacesWithRegexAsync(
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
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching multiple workspaces");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "SearchMultipleWorkspacesWithRegex",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("query_metrics")]
    [Description("Query metrics from Azure Monitor. See skills/azure/monitor/query-metrics.md only when using this tool")]
    public async Task<string> QueryMetrics(
        string resourceId,
        string metricNames,
        string startTime,
        string endTime,
        int? intervalMinutes = null,
        string? aggregations = null)
    {
        try
        {
            logger.LogDebug("Querying metrics for resource {ResourceId}", resourceId);

            var start = DateTime.Parse(startTime);
            var end = DateTime.Parse(endTime);
            var metrics = metricNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim());

            TimeSpan? interval = intervalMinutes.HasValue
                ? TimeSpan.FromMinutes(intervalMinutes.Value)
                : null;

            var aggList = !string.IsNullOrEmpty(aggregations)
                ? aggregations.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim())
                : null;

            var result = await monitorService.QueryMetricsAsync(
                resourceId, metrics, start, end, interval, aggList);

            if (result.Error is not null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.Error
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying metrics");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "QueryMetrics",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_metrics")]
    [Description("List available metrics for a resource. See skills/azure/monitor/list-metrics.md only when using this tool")]
    public async Task<string> ListMetrics(string resourceId, string? metricNamespace = null)
    {
        try
        {
            logger.LogDebug("Listing metrics for resource {ResourceId}", resourceId);
            var metrics = await monitorService.ListMetricsAsync(resourceId, metricNamespace);

            return JsonSerializer.Serialize(new
            {
                success = true,
                metrics = metrics.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing metrics for resource {ResourceId}", resourceId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListMetrics",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_application_insights")]
    [Description("List Application Insights components. See skills/azure/monitor/list-application-insights.md only when using this tool")]
    public async Task<string> ListApplicationInsights(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing Application Insights");
            var components = await monitorService.ListApplicationInsightsAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                components = components.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Application Insights");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListApplicationInsights",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_application_insights")]
    [Description("Get Application Insights component details. See skills/azure/monitor/get-application-insights.md only when using this tool")]
    public async Task<string> GetApplicationInsights(
        string resourceGroupName,
        string componentName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting Application Insights {ComponentName}", componentName);
            var component = await monitorService.GetApplicationInsightsAsync(resourceGroupName, componentName, subscriptionId);

            if (component is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Application Insights component '{componentName}' not found in resource group '{resourceGroupName}'"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                component
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Application Insights {ComponentName}", componentName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetApplicationInsights",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_alerts")]
    [Description("List alert rules. See skills/azure/monitor/list-alerts.md only when using this tool")]
    public async Task<string> ListAlerts(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing alerts");
            var alerts = await monitorService.ListAlertsAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                alerts = alerts.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing alerts");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListAlerts",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_alert")]
    [Description("Create alert rule. See skills/azure/monitor/create-alert.md only when using this tool")]
    public async Task<string> CreateAlert(
        string subscriptionId,
        string resourceGroupName,
        string alertName,
        string description,
        string severity,
        string workspaceId,
        string query,
        string evaluationFrequency,
        string windowSize)
    {
        try
        {
            logger.LogDebug("Creating alert {AlertName}", alertName);

            var alert = await monitorService.CreateAlertAsync(
                subscriptionId,
                resourceGroupName,
                alertName,
                description,
                severity,
                workspaceId,
                query,
                evaluationFrequency,
                windowSize);

            if (alert is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Failed to create alert"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                alert
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating alert {AlertName}", alertName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "CreateAlert",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }
}
