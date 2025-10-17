using AzureServer.Services.Monitor;
using AzureServer.Services.Monitor.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitorController(IMonitorService monitorService, ILogger<MonitorController> logger) : ControllerBase
{
    [HttpPost("logs/query")]
    public async Task<ActionResult> QueryLogs([FromBody] QueryLogsRequest request)
    {
        try
        {
            var timeSpan = request.TimeRangeHours.HasValue 
                ? TimeSpan.FromHours(request.TimeRangeHours.Value) 
                : TimeSpan.FromHours(24);

            var result = await monitorService.QueryLogsAsync(request.WorkspaceId, request.Query, timeSpan);
            
            if (result.Error is not null)
                return BadRequest(new { success = false, error = result.Error });

            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying logs");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "QueryLogs", type = ex.GetType().Name });
        }
    }

    [HttpGet("logs/workspaces")]
    public async Task<ActionResult> ListLogGroups([FromQuery] string? subscriptionId = null)
    {
        try
        {
            var workspaces = await monitorService.ListLogGroupsAsync(subscriptionId);
            return Ok(new { success = true, workspaces = workspaces.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log groups");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListLogGroups", type = ex.GetType().Name });
        }
    }

    [HttpGet("logs/workspaces/{workspaceId}/streams")]
    public async Task<ActionResult> ListLogStreams(string workspaceId)
    {
        try
        {
            var streams = await monitorService.ListLogStreamsAsync(workspaceId);
            return Ok(new { success = true, streams = streams.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log streams for workspace {WorkspaceId}", workspaceId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListLogStreams", type = ex.GetType().Name });
        }
    }

    [HttpPost("logs/search/regex")]
    public async Task<ActionResult> SearchLogsWithRegex([FromBody] SearchLogsRequest request)
    {
        try
        {
            var matches = await monitorService.SearchLogsWithRegexAsync(
                request.WorkspaceId,
                request.RegexPattern,
                TimeSpan.FromHours(request.TimeRangeHours),
                request.ContextLines,
                request.CaseSensitive,
                request.MaxMatches);

            return Ok(new { success = true, matchCount = matches.Count, matches });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching logs with regex");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "SearchLogsWithRegex", type = ex.GetType().Name });
        }
    }

    [HttpPost("logs/search/regex/multiple")]
    public async Task<ActionResult> SearchMultipleWorkspacesWithRegex([FromBody] SearchMultipleWorkspacesRequest request)
    {
        try
        {
            IEnumerable<string> workspaceList = request.WorkspaceIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim()).ToList();

            var matches = await monitorService.SearchMultipleWorkspacesWithRegexAsync(
                workspaceList,
                request.RegexPattern,
                TimeSpan.FromHours(request.TimeRangeHours),
                request.ContextLines,
                request.CaseSensitive,
                request.MaxMatches,
                request.MaxWorkspaces);

            return Ok(new
            {
                success = true,
                matchCount = matches.Count,
                workspacesSearched = workspaceList.Take(request.MaxWorkspaces).Count(),
                matches
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching multiple workspaces");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "SearchMultipleWorkspacesWithRegex", type = ex.GetType().Name });
        }
    }

    [HttpPost("metrics/query")]
    public async Task<ActionResult> QueryMetrics([FromBody] QueryMetricsRequest request)
    {
        try
        {
            var start = DateTime.Parse(request.StartTime);
            var end = DateTime.Parse(request.EndTime);
            var metrics = request.MetricNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim());

            TimeSpan? interval = request.IntervalMinutes.HasValue 
                ? TimeSpan.FromMinutes(request.IntervalMinutes.Value) 
                : null;

            var aggList = !string.IsNullOrEmpty(request.Aggregations)
                ? request.Aggregations.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim())
                : null;

            var result = await monitorService.QueryMetricsAsync(
                request.ResourceId, metrics, start, end, interval, aggList);

            if (result.Error is not null)
                return BadRequest(new { success = false, error = result.Error });

            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying metrics");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "QueryMetrics", type = ex.GetType().Name });
        }
    }

    [HttpGet("metrics/list")]
    public async Task<ActionResult> ListMetrics(
        [FromQuery] string resourceId,
        [FromQuery] string? metricNamespace = null)
    {
        try
        {
            var metrics = await monitorService.ListMetricsAsync(resourceId, metricNamespace);
            return Ok(new { success = true, metrics = metrics.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing metrics for resource {ResourceId}", resourceId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListMetrics", type = ex.GetType().Name });
        }
    }

    [HttpGet("appinsights")]
    public async Task<ActionResult> ListApplicationInsights(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var components = await monitorService.ListApplicationInsightsAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, components = components.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Application Insights");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListApplicationInsights", type = ex.GetType().Name });
        }
    }

    [HttpGet("appinsights/{resourceGroupName}/{componentName}")]
    public async Task<ActionResult> GetApplicationInsights(
        string resourceGroupName,
        string componentName,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var component = await monitorService.GetApplicationInsightsAsync(resourceGroupName, componentName, subscriptionId);
            if (component is null)
                return NotFound(new { success = false, error = $"Application Insights component '{componentName}' not found in resource group '{resourceGroupName}'" });

            return Ok(new { success = true, component });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Application Insights {ComponentName}", componentName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetApplicationInsights", type = ex.GetType().Name });
        }
    }

    [HttpGet("alerts")]
    public async Task<ActionResult> ListAlerts(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var alerts = await monitorService.ListAlertsAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, alerts = alerts.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing alerts");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListAlerts", type = ex.GetType().Name });
        }
    }

    [HttpPost("alerts")]
    public async Task<ActionResult> CreateAlert([FromBody] CreateAlertRequest request)
    {
        try
        {
            var alert = await monitorService.CreateAlertAsync(
                request.SubscriptionId,
                request.ResourceGroupName,
                request.AlertName,
                request.Description,
                request.Severity,
                request.WorkspaceId,
                request.Query,
                request.EvaluationFrequency,
                request.WindowSize);

            if (alert is null)
                return BadRequest(new { success = false, error = "Failed to create alert" });

            return Ok(new { success = true, alert });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating alert {AlertName}", request.AlertName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateAlert", type = ex.GetType().Name });
        }
    }
}

public record QueryLogsRequest(string WorkspaceId, string Query, int? TimeRangeHours = null);
public record SearchLogsRequest(string WorkspaceId, string RegexPattern, int TimeRangeHours = 24, int ContextLines = 3, bool CaseSensitive = false, int MaxMatches = 100);
public record SearchMultipleWorkspacesRequest(string WorkspaceIds, string RegexPattern, int TimeRangeHours = 24, int ContextLines = 2, bool CaseSensitive = false, int MaxMatches = 100, int MaxWorkspaces = 5);
public record QueryMetricsRequest(string ResourceId, string MetricNames, string StartTime, string EndTime, int? IntervalMinutes = null, string? Aggregations = null);
public record CreateAlertRequest(string SubscriptionId, string ResourceGroupName, string AlertName, string Description, string Severity, string WorkspaceId, string Query, string EvaluationFrequency, string WindowSize);