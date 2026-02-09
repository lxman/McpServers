using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Amazon.QuickSight.Model;
using AwsServer.Core.Services.QuickSight;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AwsMcp.McpTools;

/// <summary>
/// MCP tools for AWS QuickSight operations
/// </summary>
[McpServerToolType]
public class QuickSightTools(
    QuickSightService quickSightService,
    ILogger<QuickSightTools> logger)
{
    private string? _awsAccountId;

    [McpServerTool, DisplayName("initialize_quicksight")]
    [Description("Initialize QuickSight with AWS account ID. See skills/aws/quicksight/initialize.md only when using this tool")]
    public string InitializeQuickSight(
        string awsAccountId)
    {
        try
        {
            logger.LogDebug("Initializing QuickSight with account ID {AccountId}", awsAccountId);
            _awsAccountId = awsAccountId;

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "QuickSight initialized successfully",
                awsAccountId
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing QuickSight");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_quicksight_dashboards")]
    [Description("List QuickSight dashboards. See skills/aws/quicksight/list-dashboards.md only when using this tool")]
    public async Task<string> ListQuickSightDashboards()
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Listing QuickSight dashboards");
            ListDashboardsResponse response = await quickSightService.ListDashboardsAsync(_awsAccountId!);

            return JsonSerializer.Serialize(new
            {
                success = true,
                dashboardCount = response.DashboardSummaryList.Count,
                dashboards = response.DashboardSummaryList.Select(d => new
                {
                    dashboardId = d.DashboardId,
                    name = d.Name,
                    arn = d.Arn,
                    createdTime = d.CreatedTime,
                    lastUpdatedTime = d.LastUpdatedTime,
                    publishedVersionNumber = d.PublishedVersionNumber,
                    lastPublishedTime = d.LastPublishedTime
                })
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing QuickSight dashboards");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("describe_quicksight_dashboard")]
    [Description("Describe QuickSight dashboard. See skills/aws/quicksight/describe-dashboard.md only when using this tool")]
    public async Task<string> DescribeQuickSightDashboard(
        string dashboardId)
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Describing QuickSight dashboard {DashboardId}", dashboardId);
            DescribeDashboardResponse response = await quickSightService.DescribeDashboardAsync(_awsAccountId!, dashboardId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                dashboard = new
                {
                    dashboardId = response.Dashboard.DashboardId,
                    arn = response.Dashboard.Arn,
                    name = response.Dashboard.Name,
                    version = response.Dashboard.Version?.VersionNumber,
                    createdTime = response.Dashboard.CreatedTime,
                    lastUpdatedTime = response.Dashboard.LastUpdatedTime,
                    lastPublishedTime = response.Dashboard.LastPublishedTime
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing QuickSight dashboard {DashboardId}", dashboardId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_quicksight_analyses")]
    [Description("List QuickSight analyses. See skills/aws/quicksight/list-analyses.md only when using this tool")]
    public async Task<string> ListQuickSightAnalyses()
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Listing QuickSight analyses");
            ListAnalysesResponse response = await quickSightService.ListAnalysesAsync(_awsAccountId!);

            return JsonSerializer.Serialize(new
            {
                success = true,
                analysisCount = response.AnalysisSummaryList.Count,
                analyses = response.AnalysisSummaryList.Select(a => new
                {
                    analysisId = a.AnalysisId,
                    name = a.Name,
                    arn = a.Arn,
                    status = a.Status?.Value,
                    createdTime = a.CreatedTime,
                    lastUpdatedTime = a.LastUpdatedTime
                })
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing QuickSight analyses");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_quicksight_datasets")]
    [Description("List QuickSight datasets. See skills/aws/quicksight/list-datasets.md only when using this tool")]
    public async Task<string> ListQuickSightDatasets()
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Listing QuickSight datasets");
            ListDataSetsResponse response = await quickSightService.ListDataSetsAsync(_awsAccountId!);

            return JsonSerializer.Serialize(new
            {
                success = true,
                datasetCount = response.DataSetSummaries.Count,
                datasets = response.DataSetSummaries.Select(d => new
                {
                    dataSetId = d.DataSetId,
                    name = d.Name,
                    arn = d.Arn,
                    createdTime = d.CreatedTime,
                    lastUpdatedTime = d.LastUpdatedTime,
                    importMode = d.ImportMode?.Value,
                    rowLevelPermissionDataSet = d.RowLevelPermissionDataSet,
                    columnLevelPermissionRulesApplied = d.ColumnLevelPermissionRulesApplied
                })
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing QuickSight datasets");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_quicksight_data_sources")]
    [Description("List QuickSight data sources. See skills/aws/quicksight/list-data-sources.md only when using this tool")]
    public async Task<string> ListQuickSightDataSources()
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Listing QuickSight data sources");
            ListDataSourcesResponse response = await quickSightService.ListDataSourcesAsync(_awsAccountId!);

            return JsonSerializer.Serialize(new
            {
                success = true,
                dataSourceCount = response.DataSources.Count,
                dataSources = response.DataSources.Select(ds => new
                {
                    dataSourceId = ds.DataSourceId,
                    name = ds.Name,
                    arn = ds.Arn,
                    type = ds.Type?.Value,
                    status = ds.Status?.Value,
                    createdTime = ds.CreatedTime,
                    lastUpdatedTime = ds.LastUpdatedTime,
                    errorInfo = ds.ErrorInfo == null ? null : new
                    {
                        type = ds.ErrorInfo.Type?.Value,
                        message = ds.ErrorInfo.Message
                    }
                })
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing QuickSight data sources");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("generate_embed_url")]
    [Description("Generate QuickSight dashboard embed URL. See skills/aws/quicksight/generate-embed-url.md only when using this tool")]
    public async Task<string> GenerateEmbedUrl(
        string dashboardId,
        string namespaceName = "default",
        long sessionLifetimeInMinutes = 60,
        string region = "us-east-1")
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Generating embed URL for dashboard {DashboardId}", dashboardId);

            var authorizedResourceArns = new List<string>
            {
                $"arn:aws:quicksight:{region}:{_awsAccountId}:dashboard/{dashboardId}"
            };

            GenerateEmbedUrlForAnonymousUserResponse response = await quickSightService.GenerateEmbedUrlForAnonymousUserAsync(
                _awsAccountId!,
                namespaceName,
                authorizedResourceArns,
                null,
                sessionLifetimeInMinutes);

            return JsonSerializer.Serialize(new
            {
                success = true,
                embedUrl = response.EmbedUrl,
                requestId = response.RequestId,
                expiresInMinutes = sessionLifetimeInMinutes
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating embed URL for dashboard {DashboardId}", dashboardId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("describe_quicksight_analysis")]
    [Description("Describe QuickSight analysis. See skills/aws/quicksight/describe-analysis.md only when using this tool")]
    public async Task<string> DescribeQuickSightAnalysis(string analysisId)
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Describing QuickSight analysis {AnalysisId}", analysisId);
            DescribeAnalysisResponse response = await quickSightService.DescribeAnalysisAsync(_awsAccountId!, analysisId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                analysis = new
                {
                    analysisId = response.Analysis.AnalysisId,
                    arn = response.Analysis.Arn,
                    name = response.Analysis.Name,
                    status = response.Analysis.Status?.Value,
                    createdTime = response.Analysis.CreatedTime,
                    lastUpdatedTime = response.Analysis.LastUpdatedTime,
                    errors = response.Analysis.Errors?.Select(e => new
                    {
                        type = e.Type?.Value,
                        message = e.Message
                    })
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing QuickSight analysis {AnalysisId}", analysisId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("describe_quicksight_dataset")]
    [Description("Describe QuickSight dataset. See skills/aws/quicksight/describe-dataset.md only when using this tool")]
    public async Task<string> DescribeQuickSightDataset(string dataSetId)
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Describing QuickSight dataset {DataSetId}", dataSetId);
            DescribeDataSetResponse response = await quickSightService.DescribeDataSetAsync(_awsAccountId!, dataSetId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                dataset = new
                {
                    dataSetId = response.DataSet.DataSetId,
                    arn = response.DataSet.Arn,
                    name = response.DataSet.Name,
                    createdTime = response.DataSet.CreatedTime,
                    lastUpdatedTime = response.DataSet.LastUpdatedTime,
                    importMode = response.DataSet.ImportMode?.Value,
                    consumedSpiceCapacityInBytes = response.DataSet.ConsumedSpiceCapacityInBytes,
                    rowLevelPermissionDataSet = response.DataSet.RowLevelPermissionDataSet
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing QuickSight dataset {DataSetId}", dataSetId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("describe_quicksight_data_source")]
    [Description("Describe QuickSight data source. See skills/aws/quicksight/describe-data-source.md only when using this tool")]
    public async Task<string> DescribeQuickSightDataSource(string dataSourceId)
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Describing QuickSight data source {DataSourceId}", dataSourceId);
            DescribeDataSourceResponse response = await quickSightService.DescribeDataSourceAsync(_awsAccountId!, dataSourceId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                dataSource = new
                {
                    dataSourceId = response.DataSource.DataSourceId,
                    arn = response.DataSource.Arn,
                    name = response.DataSource.Name,
                    type = response.DataSource.Type?.Value,
                    status = response.DataSource.Status?.Value,
                    createdTime = response.DataSource.CreatedTime,
                    lastUpdatedTime = response.DataSource.LastUpdatedTime,
                    errorInfo = response.DataSource.ErrorInfo == null ? null : new
                    {
                        type = response.DataSource.ErrorInfo.Type?.Value,
                        message = response.DataSource.ErrorInfo.Message
                    }
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing QuickSight data source {DataSourceId}", dataSourceId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_quicksight_users")]
    [Description("List QuickSight users. See skills/aws/quicksight/list-users.md only when using this tool")]
    public async Task<string> ListQuickSightUsers(string namespaceName = "default")
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Listing QuickSight users in namespace {Namespace}", namespaceName);
            ListUsersResponse response = await quickSightService.ListUsersAsync(_awsAccountId!, namespaceName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                userCount = response.UserList.Count,
                users = response.UserList.Select(u => new
                {
                    userName = u.UserName,
                    email = u.Email,
                    role = u.Role?.Value,
                    identityType = u.IdentityType?.Value,
                    active = u.Active,
                    arn = u.Arn,
                    principalId = u.PrincipalId
                })
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing QuickSight users");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("describe_quicksight_user")]
    [Description("Describe QuickSight user. See skills/aws/quicksight/describe-user.md only when using this tool")]
    public async Task<string> DescribeQuickSightUser(string userName, string namespaceName = "default")
    {
        try
        {
            EnsureAccountIdSet();
            logger.LogDebug("Describing QuickSight user {UserName} in namespace {Namespace}", userName, namespaceName);
            DescribeUserResponse response = await quickSightService.DescribeUserAsync(_awsAccountId!, userName, namespaceName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                user = new
                {
                    userName = response.User.UserName,
                    email = response.User.Email,
                    role = response.User.Role?.Value,
                    identityType = response.User.IdentityType?.Value,
                    active = response.User.Active,
                    arn = response.User.Arn,
                    principalId = response.User.PrincipalId,
                    customPermissionsName = response.User.CustomPermissionsName
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing QuickSight user {UserName}", userName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    private void EnsureAccountIdSet()
    {
        if (string.IsNullOrEmpty(_awsAccountId))
        {
            throw new InvalidOperationException("AWS Account ID not set. Please call initialize_quicksight first.");
        }
    }
}