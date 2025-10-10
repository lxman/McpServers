using System.ComponentModel;
using System.Text.Json;
using Amazon.QuickSight.Model;
using AwsMcp.QuickSight;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

[McpServerToolType]
public class QuickSightDashboardTools(QuickSightService quickSightService)
{
    [McpServerTool]
    [Description("List all QuickSight dashboards in an AWS account")]
    public async Task<string> ListDashboards(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            ListDashboardsResponse response = await quickSightService.ListDashboardsAsync(awsAccountId, maxResults);
            
            var dashboards = response.DashboardSummaryList.Select(d => new
            {
                d.DashboardId,
                d.Name,
                d.Arn,
                d.CreatedTime,
                d.LastUpdatedTime,
                d.PublishedVersionNumber,
                d.LastPublishedTime
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                dashboards,
                count = response.DashboardSummaryList.Count,
                nextToken = response.NextToken
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get detailed information about a specific QuickSight dashboard")]
    public async Task<string> DescribeDashboard(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Dashboard ID")]
        string dashboardId)
    {
        try
        {
            DescribeDashboardResponse response = await quickSightService.DescribeDashboardAsync(awsAccountId, dashboardId);
            
            var dashboard = response.Dashboard;
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                dashboard = new
                {
                    dashboard.DashboardId,
                    dashboard.Name,
                    dashboard.Arn,
                    dashboard.CreatedTime,
                    dashboard.LastUpdatedTime,
                    dashboard.LastPublishedTime,
                    Version = dashboard.Version != null ? new
                    {
                        dashboard.Version.VersionNumber,
                        Status = dashboard.Version.Status?.Value,
                        dashboard.Version.CreatedTime,
                        Errors = dashboard.Version.Errors?.Select(e => new
                        {
                            Type = e.Type?.Value, e.Message
                        })
                    } : null
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Search QuickSight dashboards with filters")]
    public async Task<string> SearchDashboards(
        [Description("AWS Account ID")]
        string awsAccountId,
        [Description("Search filters as JSON array (e.g., '[{\"Operator\":\"StringEquals\",\"Name\":\"QUICKSIGHT_USER\",\"Value\":\"arn:aws:quicksight:us-east-1:123456789012:user/default/username\"}]')")]
        string filtersJson,
        [Description("Maximum number of results to return (default: 100)")]
        int maxResults = 100)
    {
        try
        {
            var filters = JsonSerializer.Deserialize<List<DashboardSearchFilter>>(filtersJson);
            if (filters == null || filters.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "At least one search filter must be provided"
                });
            }

            SearchDashboardsResponse response = await quickSightService.SearchDashboardsAsync(
                awsAccountId,
                filters,
                maxResults);
            
            var dashboards = response.DashboardSummaryList.Select(d => new
            {
                d.DashboardId,
                d.Name,
                d.Arn,
                d.CreatedTime,
                d.LastUpdatedTime,
                d.PublishedVersionNumber,
                d.LastPublishedTime
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                dashboards,
                count = response.DashboardSummaryList.Count,
                nextToken = response.NextToken
            });
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Invalid JSON format for filters: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
