using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using AzureServer.Core.Services.Monitor;
using AzureServer.Core.Services.Monitor.Models;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Models;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Monitor operations
/// </summary>
[McpServerToolType]
public class MonitorTools(
    IMonitorService monitorService,
    ILogger<MonitorTools> logger,
    OutputGuard outputGuard)
{
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

            TimeSpan timeSpan = timeRangeHours.HasValue
                ? TimeSpan.FromHours(timeRangeHours.Value)
                : TimeSpan.FromHours(24);

            var limitedQuery = $"{query} | take {maxResults ?? 1000}";

            LogQueryResult result = await monitorService.QueryLogsAsync(workspaceId, limitedQuery, timeSpan);

            if (result.Error is not null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.Error
                }, SerializerOptions.JsonOptionsIndented);
            }

            (long? count, string confidence) estimate = await monitorService.GetLogCountEstimateAsync(
                workspaceId,
                query,
                timeSpan,
                useQuickEstimate);

            int totalRows = result.Tables.Sum(t => t.Rows.Count);
            result.Pagination = monitorService.CalculatePaginationMetadata(
                totalRows,
                maxResults ?? 1000,
                1,
                false,
                estimate.count,
                estimate.confidence);

            string serialized = JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, SerializerOptions.JsonOptionsIndented);

            // Check response size - Azure Monitor log queries can return large result sets
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(serialized, "query_logs");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Azure Monitor log query returned {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Reduce maxResults parameter (currently {maxResults}, try 500 or 250)\n" +
                    "  2. Add more selective filters to your KQL query\n" +
                    "  3. Use 'project' clause to select fewer columns\n" +
                    "  4. Reduce time range with timeRangeHours parameter\n" +
                    "  5. Use aggregation functions (summarize, count) instead of raw results",
                    new {
                        currentMaxResults = maxResults ?? 1000,
                        suggestedMaxResults = Math.Max(100, (maxResults ?? 1000) / 4),
                        totalRows,
                        workspaceId
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying logs from workspace {WorkspaceId}", workspaceId);
            return ex.ToErrorResponse(outputGuard, errorCode: "QUERY_LOGS_FAILED");
        }
    }

    [McpServerTool, DisplayName("list_log_workspaces")]
    [Description("List Azure Monitor Log Analytics workspaces. See skills/azure/monitor/list-log-workspaces.md only when using this tool")]
    public async Task<string> ListLogWorkspaces(string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing log workspaces");
            IEnumerable<string?> workspaces = await monitorService.ListLogGroupsAsync(subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                workspaces = workspaces.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log workspaces");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("list_log_streams")]
    [Description("List log streams in a workspace. See skills/azure/monitor/list-log-streams.md only when using this tool")]
    public async Task<string> ListLogStreams(string workspaceId)
    {
        try
        {
            logger.LogDebug("Listing log streams for workspace {WorkspaceId}", workspaceId);
            IEnumerable<string> streams = await monitorService.ListLogStreamsAsync(workspaceId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                streams = streams.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log streams for workspace {WorkspaceId}", workspaceId);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
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
            logger.LogError(ex, "Error searching logs with regex");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
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
            logger.LogError(ex, "Error searching multiple workspaces");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
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
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.Error
                }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying metrics");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("list_metrics")]
    [Description("List available metrics for a resource. See skills/azure/monitor/list-metrics.md only when using this tool")]
    public async Task<string> ListMetrics(string resourceId, string? metricNamespace = null)
    {
        try
        {
            logger.LogDebug("Listing metrics for resource {ResourceId}", resourceId);
            IEnumerable<string> metrics = await monitorService.ListMetricsAsync(resourceId, metricNamespace);

            return JsonSerializer.Serialize(new
            {
                success = true,
                metrics = metrics.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing metrics for resource {ResourceId}", resourceId);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("list_application_insights")]
    [Description("List Application Insights components. See skills/azure/monitor/list-application-insights.md only when using this tool")]
    public async Task<string> ListApplicationInsights(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing Application Insights");
            IEnumerable<ApplicationInsightsDto> components = await monitorService.ListApplicationInsightsAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                components = components.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Application Insights");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
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
            ApplicationInsightsDto? component = await monitorService.GetApplicationInsightsAsync(resourceGroupName, componentName, subscriptionId);

            if (component is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Application Insights component '{componentName}' not found in resource group '{resourceGroupName}'"
                }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                component
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Application Insights {ComponentName}", componentName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("list_alerts")]
    [Description("List alert rules. See skills/azure/monitor/list-alerts.md only when using this tool")]
    public async Task<string> ListAlerts(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing alerts");
            IEnumerable<AlertRuleDto> alerts = await monitorService.ListAlertsAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                alerts = alerts.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing alerts");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
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

            AlertRuleDto? alert = await monitorService.CreateAlertAsync(
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
                }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                alert
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating alert {AlertName}", alertName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }
}
