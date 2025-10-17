using AwsServer.Configuration;
using AwsServer.Controllers.Requests;
using AwsServer.QuickSight;
using Microsoft.AspNetCore.Mvc;

namespace AwsServer.Controllers;

[ApiController]
[Route("api/quicksight")]
public class QuickSightController(QuickSightService quickSightService) : ControllerBase
{
    private string? _awsAccountId;

    /// <summary>
    /// Initialize QuickSight service with AWS credentials
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] InitializeQuickSightRequest request)
    {
        try
        {
            var success = await quickSightService.InitializeAsync(request.Config);
            if (success)
            {
                _awsAccountId = request.AwsAccountId;
            }
            return Ok(new { success, message = success ? "QuickSight service initialized successfully" : "Failed to initialize QuickSight service" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List QuickSight dashboards
    /// </summary>
    [HttpGet("dashboards")]
    public async Task<IActionResult> ListDashboards()
    {
        try
        {
            EnsureAccountIdSet();
            var dashboards = await quickSightService.ListDashboardsAsync(_awsAccountId!);
            return Ok(new { success = true, dashboardCount = dashboards.DashboardSummaryList.Count, dashboards });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe a QuickSight dashboard
    /// </summary>
    [HttpGet("dashboards/{dashboardId}")]
    public async Task<IActionResult> DescribeDashboard(string dashboardId)
    {
        try
        {
            EnsureAccountIdSet();
            var dashboard = await quickSightService.DescribeDashboardAsync(_awsAccountId!, dashboardId);
            return Ok(new { success = true, dashboard });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List QuickSight analyses
    /// </summary>
    [HttpGet("analyses")]
    public async Task<IActionResult> ListAnalyses()
    {
        try
        {
            EnsureAccountIdSet();
            var analyses = await quickSightService.ListAnalysesAsync(_awsAccountId!);
            return Ok(new { success = true, analysisCount = analyses.AnalysisSummaryList.Count, analyses });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe a QuickSight analysis
    /// </summary>
    [HttpGet("analyses/{analysisId}")]
    public async Task<IActionResult> DescribeAnalysis(string analysisId)
    {
        try
        {
            EnsureAccountIdSet();
            var analysis = await quickSightService.DescribeAnalysisAsync(_awsAccountId!, analysisId);
            return Ok(new { success = true, analysis });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List QuickSight datasets
    /// </summary>
    [HttpGet("datasets")]
    public async Task<IActionResult> ListDatasets()
    {
        try
        {
            EnsureAccountIdSet();
            var datasets = await quickSightService.ListDataSetsAsync(_awsAccountId!);
            return Ok(new { success = true, datasetCount = datasets.DataSetSummaries.Count, datasets });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe a QuickSight dataset
    /// </summary>
    [HttpGet("datasets/{dataSetId}")]
    public async Task<IActionResult> DescribeDataset(string dataSetId)
    {
        try
        {
            EnsureAccountIdSet();
            var dataset = await quickSightService.DescribeDataSetAsync(_awsAccountId!, dataSetId);
            return Ok(new { success = true, dataset });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List QuickSight data sources
    /// </summary>
    [HttpGet("datasources")]
    public async Task<IActionResult> ListDataSources()
    {
        try
        {
            EnsureAccountIdSet();
            var dataSources = await quickSightService.ListDataSourcesAsync(_awsAccountId!);
            return Ok(new { success = true, dataSourceCount = dataSources.DataSources.Count, dataSources });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe a QuickSight data source
    /// </summary>
    [HttpGet("datasources/{dataSourceId}")]
    public async Task<IActionResult> DescribeDataSource(string dataSourceId)
    {
        try
        {
            EnsureAccountIdSet();
            var dataSource = await quickSightService.DescribeDataSourceAsync(_awsAccountId!, dataSourceId);
            return Ok(new { success = true, dataSource });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate an embed URL for a QuickSight dashboard for anonymous user
    /// </summary>
    [HttpPost("dashboards/{dashboardId}/embed-url")]
    public async Task<IActionResult> GenerateDashboardEmbedUrl(
        string dashboardId,
        [FromBody] GenerateEmbedUrlRequest request)
    {
        try
        {
            EnsureAccountIdSet();
            var response = await quickSightService.GenerateEmbedUrlForAnonymousUserAsync(
                _awsAccountId!,
                request.Namespace,
                request.AuthorizedResourceArns ??
                [
                    $"arn:aws:quicksight:{request.Region ?? "us-east-1"}:{_awsAccountId}:dashboard/{dashboardId}"
                ],
                null,
                request.SessionLifetimeInMinutes);
            return Ok(new { success = true, embedUrl = response.EmbedUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List QuickSight users
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] string @namespace = "default")
    {
        try
        {
            EnsureAccountIdSet();
            var users = await quickSightService.ListUsersAsync(_awsAccountId!, @namespace);
            return Ok(new { success = true, userCount = users.UserList.Count, users });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe a QuickSight user
    /// </summary>
    [HttpGet("users/{userName}")]
    public async Task<IActionResult> DescribeUser(string userName, [FromQuery] string @namespace = "default")
    {
        try
        {
            EnsureAccountIdSet();
            var user = await quickSightService.DescribeUserAsync(_awsAccountId!, userName, @namespace);
            return Ok(new { success = true, user });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private void EnsureAccountIdSet()
    {
        if (string.IsNullOrEmpty(_awsAccountId))
        {
            throw new InvalidOperationException("AWS Account ID is not set. Please call Initialize first.");
        }
    }
}



