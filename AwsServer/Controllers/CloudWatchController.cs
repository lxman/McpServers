using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs.Model;
using AwsServer.CloudWatch;
using AwsServer.Configuration;
using Microsoft.AspNetCore.Mvc;
using GetMetricStatisticsRequest = AwsServer.Controllers.Requests.GetMetricStatisticsRequest;
using PutMetricDataRequest = AwsServer.Controllers.Requests.PutMetricDataRequest;

namespace AwsServer.Controllers;

[ApiController]
[Route("api/cloudwatch")]
public class CloudWatchController(CloudWatchService cloudWatchService) : ControllerBase
{
    /// <summary>
    /// Initialize CloudWatch service with AWS credentials
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] AwsConfiguration config)
    {
        try
        {
            bool success = await cloudWatchService.InitializeAsync(config);
            return Ok(new { success, message = success ? "CloudWatch service initialized successfully" : "Failed to initialize CloudWatch service" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List CloudWatch log groups
    /// </summary>
    [HttpGet("log-groups")]
    public async Task<IActionResult> ListLogGroups([FromQuery] string? prefix = null, [FromQuery] int limit = 50)
    {
        try
        {
            List<LogGroup> logGroups = await cloudWatchService.ListLogGroupsAsync(prefix, limit);
            return Ok(new { success = true, logGroupCount = logGroups.Count, logGroups });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List log streams in a log group
    /// </summary>
    [HttpGet("log-groups/{logGroupName}/streams")]
    public async Task<IActionResult> ListLogStreams(string logGroupName, [FromQuery] int limit = 50)
    {
        try
        {
            List<LogStream> logStreams = await cloudWatchService.ListLogStreamsAsync(logGroupName, limit);
            return Ok(new { success = true, logStreamCount = logStreams.Count, logStreams });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get log events from a log stream
    /// </summary>
    [HttpGet("log-groups/{logGroupName}/streams/{logStreamName}/events")]
    public async Task<IActionResult> GetLogEvents(
        string logGroupName,
        string logStreamName,
        [FromQuery] int limit = 100,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        try
        {
            List<OutputLogEvent> events = await cloudWatchService.GetLogEventsAsync(logGroupName, logStreamName, startTime, endTime, limit);
            return Ok(new { success = true, eventCount = events.Count, events });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get CloudWatch metric statistics
    /// </summary>
    [HttpGet("metrics/statistics")]
    public async Task<IActionResult> GetMetricStatistics([FromQuery] GetMetricStatisticsRequest request)
    {
        try
        {
            List<Dimension>? dimensions = request.Dimensions?.Select(kvp => new Dimension { Name = kvp.Key, Value = kvp.Value }).ToList();
            List<Datapoint> statistics = await cloudWatchService.GetMetricStatisticsAsync(
                request.Namespace,
                request.MetricName,
                request.StartTime,
                request.EndTime,
                request.Period,
                request.Statistics ?? [],
                dimensions);
            return Ok(new { success = true, statistics });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List CloudWatch metrics
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> ListMetrics(
        [FromQuery] string? @namespace = null,
        [FromQuery] string? metricName = null)
    {
        try
        {
            List<Metric> metrics = await cloudWatchService.ListMetricsAsync(@namespace, metricName);
            return Ok(new { success = true, metricCount = metrics.Count, metrics });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Put metric data to CloudWatch
    /// </summary>
    [HttpPost("metrics")]
    public async Task<IActionResult> PutMetricData([FromBody] PutMetricDataRequest request)
    {
        try
        {
            List<Dimension> dimensions =
                (from key in request.Dimensions?.Keys
                    select new Dimension { Name = key, Value = request.Dimensions?[key] }
                    ).ToList();
            await cloudWatchService.PutMetricDataAsync(
                request.Namespace,
                [
                    new MetricDatum
                    {
                        MetricName = request.MetricName,
                        Value = request.Value,
                        Unit = request.Unit,
                        Dimensions = dimensions
                    }
                ]);
            return Ok(new { success = true, message = "Metric data published successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}