using Amazon.CloudWatch.Model;
using AwsServer.Core.Services.CloudWatch;
using Microsoft.AspNetCore.Mvc;

namespace AwsServer.Controllers;

/// <summary>
/// CloudWatch Metrics API - modern, pagination-first design.
/// All endpoints support AWS native pagination tokens.
/// Supports both legacy GetMetricStatistics and modern GetMetricData APIs.
/// </summary>
[ApiController]
[Route("api/cloudwatch/metrics")]
public class CloudWatchMetricsController(
    CloudWatchMetricsService service,
    ILogger<CloudWatchMetricsController> logger)
    : ControllerBase
{
    #region Metrics Discovery

    /// <summary>
    /// List available metrics with pagination.
    /// 
    /// GET /api/cloudwatch/metrics?namespace=AWS/EC2&maxRecords=100
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MetricsListResponse), 200)]
    public async Task<IActionResult> ListMetrics(
        [FromQuery] string? @namespace = null,
        [FromQuery] string? metricName = null,
        [FromQuery] int maxRecords = 500,
        [FromQuery] string? nextToken = null)
    {
        try
        {
            ListMetricsResponse response = await service.ListMetricsAsync(
                @namespace, metricName, null, maxRecords, nextToken);
            
            return Ok(new MetricsListResponse
            {
                Metrics = response.Metrics.Select(m => new MetricDto
                {
                    Namespace = m.Namespace,
                    MetricName = m.MetricName,
                    Dimensions = m.Dimensions?.ToDictionary(d => d.Name, d => d.Value)
                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken),
                TotalReturned = response.Metrics.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing metrics");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// List all available namespaces.
    /// 
    /// GET /api/cloudwatch/metrics/namespaces
    /// </summary>
    [HttpGet("namespaces")]
    [ProducesResponseType(typeof(NamespacesResponse), 200)]
    public async Task<IActionResult> ListNamespaces()
    {
        try
        {
            List<string> namespaces = await service.ListNamespacesAsync();
            
            return Ok(new NamespacesResponse
            {
                Namespaces = namespaces,
                Count = namespaces.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing namespaces");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    #endregion

    #region Metric Statistics (Legacy API)

    /// <summary>
    /// Get metric statistics using the legacy API.
    /// For new applications, prefer the /data endpoint which uses GetMetricData.
    /// 
    /// POST /api/cloudwatch/metrics/statistics
    /// Body: { "namespace": "AWS/EC2", "metricName": "CPUUtilization", ... }
    /// </summary>
    [HttpPost("statistics")]
    [ProducesResponseType(typeof(MetricStatisticsResponse), 200)]
    public async Task<IActionResult> GetMetricStatistics([FromBody] GetMetricStatisticsRequest request)
    {
        try
        {
            List<Dimension>? dimensions = request.Dimensions?
                .Select(kvp => new Dimension { Name = kvp.Key, Value = kvp.Value })
                .ToList();
            
            GetMetricStatisticsResponse response = await service.GetMetricStatisticsAsync(
                request.Namespace,
                request.MetricName,
                request.StartTime,
                request.EndTime,
                request.Period,
                request.Statistics,
                dimensions,
                request.Unit);
            
            return Ok(new MetricStatisticsResponse
            {
                Label = response.Label,
                Datapoints = response.Datapoints.OrderBy(d => d.Timestamp).Select(d => new DatapointDto
                {
                    Timestamp = d.Timestamp,
                    Average = d.Average,
                    Sum = d.Sum,
                    Minimum = d.Minimum,
                    Maximum = d.Maximum,
                    SampleCount = d.SampleCount,
                    Unit = d.Unit?.Value
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metric statistics");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    #endregion

    #region Metric Data (Modern API)

    /// <summary>
    /// Get metric data using the modern GetMetricData API.
    /// Supports multiple metrics, math expressions, and more efficient queries.
    /// 
    /// POST /api/cloudwatch/metrics/data
    /// Body: { "queries": [...], "startTime": "2025-10-19T00:00:00Z", "endTime": "2025-10-19T23:59:59Z" }
    /// </summary>
    [HttpPost("data")]
    [ProducesResponseType(typeof(MetricDataResponse), 200)]
    public async Task<IActionResult> GetMetricData([FromBody] GetMetricDataRequestDto request)
    {
        try
        {
            List<MetricDataQuery> queries = request.Queries.Select(q => new MetricDataQuery
            {
                Id = q.Id,
                MetricStat = q.MetricStat != null ? new MetricStat
                {
                    Metric = new Metric
                    {
                        Namespace = q.MetricStat.Metric.Namespace,
                        MetricName = q.MetricStat.Metric.MetricName,
                        Dimensions = q.MetricStat.Metric.Dimensions?
                            .Select(kvp => new Dimension { Name = kvp.Key, Value = kvp.Value })
                            .ToList()
                    },
                    Period = q.MetricStat.Period,
                    Stat = q.MetricStat.Stat,
                    Unit = q.MetricStat.Unit
                } : null,
                Expression = q.Expression,
                Label = q.Label,
                ReturnData = q.ReturnData ?? true
            }).ToList();
            
            GetMetricDataResponse response = await service.GetMetricDataAsync(
                queries,
                request.StartTime,
                request.EndTime,
                request.NextToken,
                request.MaxDatapoints ?? 100800);
            
            return Ok(new MetricDataResponse
            {
                MetricDataResults = response.MetricDataResults.Select(r => new MetricDataResultDto
                {
                    Id = r.Id,
                    Label = r.Label,
                    Timestamps = r.Timestamps,
                    Values = r.Values,
                    StatusCode = r.StatusCode?.Value
                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metric data");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    #endregion

    #region Publishing Metrics

    /// <summary>
    /// Publish custom metric data to CloudWatch.
    /// 
    /// POST /api/cloudwatch/metrics/publish
    /// Body: { "namespace": "MyApp", "metricData": [...] }
    /// </summary>
    [HttpPost("publish")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> PutMetricData([FromBody] PutMetricDataRequestDto request)
    {
        try
        {
            List<MetricDatum> metricData = request.MetricData.Select(m => new MetricDatum
            {
                MetricName = m.MetricName,
                Value = m.Value,
                Timestamp = m.Timestamp ?? DateTime.UtcNow,
                Unit = m.Unit,
                Dimensions = m.Dimensions?
                    .Select(kvp => new Dimension { Name = kvp.Key, Value = kvp.Value })
                    .ToList(),
                StatisticValues = m.StatisticValues != null ? new StatisticSet
                {
                    SampleCount = m.StatisticValues.SampleCount,
                    Sum = m.StatisticValues.Sum,
                    Minimum = m.StatisticValues.Minimum,
                    Maximum = m.StatisticValues.Maximum
                } : null
            }).ToList();
            
            await service.PutMetricDataAsync(request.Namespace, metricData);
            
            return Ok(new { message = "Metric data published successfully", count = metricData.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing metric data");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    #endregion

    #region Alarms

    /// <summary>
    /// List metric alarms with pagination.
    /// 
    /// GET /api/cloudwatch/metrics/alarms?stateValue=ALARM&maxRecords=50
    /// </summary>
    [HttpGet("alarms")]
    [ProducesResponseType(typeof(AlarmsListResponse), 200)]
    public async Task<IActionResult> ListAlarms(
        [FromQuery] string? alarmNamePrefix = null,
        [FromQuery] string? stateValue = null,
        [FromQuery] int maxRecords = 100,
        [FromQuery] string? nextToken = null)
    {
        try
        {
            DescribeAlarmsResponse response = await service.ListAlarmsAsync(
                alarmNamePrefix, null, stateValue, null, maxRecords, nextToken);
            
            return Ok(new AlarmsListResponse
            {
                MetricAlarms = response.MetricAlarms.Select(a => new MetricAlarmDto
                {
                    AlarmName = a.AlarmName,
                    AlarmDescription = a.AlarmDescription,
                    MetricName = a.MetricName,
                    Namespace = a.Namespace,
                    Statistic = a.Statistic?.Value,
                    Period = a.Period,
                    EvaluationPeriods = a.EvaluationPeriods,
                    Threshold = a.Threshold,
                    ComparisonOperator = a.ComparisonOperator?.Value,
                    StateValue = a.StateValue?.Value,
                    StateReason = a.StateReason,
                    StateUpdatedTimestamp = a.StateUpdatedTimestamp,
                    ActionsEnabled = a.ActionsEnabled,
                    AlarmActions = a.AlarmActions,
                    Dimensions = a.Dimensions?.ToDictionary(d => d.Name, d => d.Value)
                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken),
                TotalReturned = response.MetricAlarms.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing alarms");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Create or update a metric alarm.
    /// 
    /// PUT /api/cloudwatch/metrics/alarms
    /// Body: { "alarmName": "HighCPU", "metricName": "CPUUtilization", ... }
    /// </summary>
    [HttpPut("alarms")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> PutMetricAlarm([FromBody] PutMetricAlarmRequestDto request)
    {
        try
        {
            List<Dimension>? dimensions = request.Dimensions?
                .Select(kvp => new Dimension { Name = kvp.Key, Value = kvp.Value })
                .ToList();
            
            await service.PutMetricAlarmAsync(
                request.AlarmName,
                request.AlarmDescription ?? string.Empty,
                request.MetricName,
                request.Namespace,
                request.Statistic,
                request.Period,
                request.EvaluationPeriods,
                request.Threshold,
                request.ComparisonOperator,
                dimensions,
                request.AlarmActions,
                request.OkActions,
                request.InsufficientDataActions,
                request.Unit,
                request.ActionsEnabled ?? true,
                request.DatapointsToAlarm,
                request.TreatMissingData);
            
            return Ok(new { message = "Alarm created/updated successfully", alarmName = request.AlarmName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating/updating alarm");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Delete metric alarms.
    /// 
    /// DELETE /api/cloudwatch/metrics/alarms
    /// Body: { "alarmNames": ["alarm1", "alarm2"] }
    /// </summary>
    [HttpDelete("alarms")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> DeleteAlarms([FromBody] DeleteAlarmsRequestDto request)
    {
        try
        {
            await service.DeleteAlarmsAsync(request.AlarmNames);
            
            return Ok(new { message = "Alarms deleted successfully", count = request.AlarmNames.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting alarms");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Enable alarm actions.
    /// 
    /// POST /api/cloudwatch/metrics/alarms/enable
    /// Body: { "alarmNames": ["alarm1", "alarm2"] }
    /// </summary>
    [HttpPost("alarms/enable")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> EnableAlarmActions([FromBody] AlarmNamesRequestDto request)
    {
        try
        {
            await service.EnableAlarmActionsAsync(request.AlarmNames);
            
            return Ok(new { message = "Alarm actions enabled", count = request.AlarmNames.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enabling alarm actions");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Disable alarm actions.
    /// 
    /// POST /api/cloudwatch/metrics/alarms/disable
    /// Body: { "alarmNames": ["alarm1", "alarm2"] }
    /// </summary>
    [HttpPost("alarms/disable")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> DisableAlarmActions([FromBody] AlarmNamesRequestDto request)
    {
        try
        {
            await service.DisableAlarmActionsAsync(request.AlarmNames);
            
            return Ok(new { message = "Alarm actions disabled", count = request.AlarmNames.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disabling alarm actions");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Get alarm history with pagination.
    /// 
    /// GET /api/cloudwatch/metrics/alarms/history?alarmName=HighCPU&maxRecords=50
    /// </summary>
    [HttpGet("alarms/history")]
    [ProducesResponseType(typeof(AlarmHistoryResponse), 200)]
    public async Task<IActionResult> GetAlarmHistory(
        [FromQuery] string? alarmName = null,
        [FromQuery] string? historyItemType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int maxRecords = 100,
        [FromQuery] string? nextToken = null)
    {
        try
        {
            DescribeAlarmHistoryResponse response = await service.DescribeAlarmHistoryAsync(
                alarmName, null, historyItemType, startDate, endDate, maxRecords, nextToken);
            
            return Ok(new AlarmHistoryResponse
            {
                AlarmHistoryItems = response.AlarmHistoryItems.Select(h => new AlarmHistoryItemDto
                {
                    AlarmName = h.AlarmName,
                    AlarmType = h.AlarmType?.Value,
                    Timestamp = h.Timestamp,
                    HistoryItemType = h.HistoryItemType?.Value,
                    HistorySummary = h.HistorySummary,
                    HistoryData = h.HistoryData
                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken),
                TotalReturned = response.AlarmHistoryItems.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting alarm history");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    #endregion
}

#region Request/Response DTOs

public record MetricsListResponse
{
    public List<MetricDto> Metrics { get; init; } = [];
    public string? NextToken { get; init; }
    public bool HasMore { get; init; }
    public int TotalReturned { get; init; }
}

public record MetricDto
{
    public string? Namespace { get; init; }
    public string? MetricName { get; init; }
    public Dictionary<string, string>? Dimensions { get; init; }
}

public record NamespacesResponse
{
    public List<string> Namespaces { get; init; } = [];
    public int Count { get; init; }
}

public record GetMetricStatisticsRequest
{
    public required string Namespace { get; init; }
    public required string MetricName { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required int Period { get; init; }
    public required List<string> Statistics { get; init; }
    public Dictionary<string, string>? Dimensions { get; init; }
    public string? Unit { get; init; }
}

public record MetricStatisticsResponse
{
    public string? Label { get; init; }
    public List<DatapointDto> Datapoints { get; init; } = [];
}

public record DatapointDto
{
    public DateTime? Timestamp { get; init; }
    public double? Average { get; init; }
    public double? Sum { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public double? SampleCount { get; init; }
    public string? Unit { get; init; }
}

public record GetMetricDataRequestDto
{
    public required List<MetricDataQueryDto> Queries { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public string? NextToken { get; init; }
    public int? MaxDatapoints { get; init; }
}

public record MetricDataQueryDto
{
    public required string Id { get; init; }
    public MetricStatDto? MetricStat { get; init; }
    public string? Expression { get; init; }
    public string? Label { get; init; }
    public bool? ReturnData { get; init; }
}

public record MetricStatDto
{
    public required MetricDto Metric { get; init; }
    public required int Period { get; init; }
    public required string Stat { get; init; }
    public string? Unit { get; init; }
}

public record MetricDataResponse
{
    public List<MetricDataResultDto> MetricDataResults { get; init; } = [];
    public string? NextToken { get; init; }
    public bool HasMore { get; init; }
}

public record MetricDataResultDto
{
    public string? Id { get; init; }
    public string? Label { get; init; }
    public List<DateTime> Timestamps { get; init; } = [];
    public List<double> Values { get; init; } = [];
    public string? StatusCode { get; init; }
}

public record PutMetricDataRequestDto
{
    public required string Namespace { get; init; }
    public required List<MetricDatumDto> MetricData { get; init; }
}

public record MetricDatumDto
{
    public required string MetricName { get; init; }
    public double? Value { get; init; }
    public DateTime? Timestamp { get; init; }
    public string? Unit { get; init; }
    public Dictionary<string, string>? Dimensions { get; init; }
    public StatisticValuesDto? StatisticValues { get; init; }
}

public record StatisticValuesDto
{
    public required double SampleCount { get; init; }
    public required double Sum { get; init; }
    public required double Minimum { get; init; }
    public required double Maximum { get; init; }
}

public record AlarmsListResponse
{
    public List<MetricAlarmDto> MetricAlarms { get; init; } = [];
    public string? NextToken { get; init; }
    public bool HasMore { get; init; }
    public int TotalReturned { get; init; }
}

public record MetricAlarmDto
{
    public string? AlarmName { get; init; }
    public string? AlarmDescription { get; init; }
    public string? MetricName { get; init; }
    public string? Namespace { get; init; }
    public string? Statistic { get; init; }
    public int? Period { get; init; }
    public int? EvaluationPeriods { get; init; }
    public double? Threshold { get; init; }
    public string? ComparisonOperator { get; init; }
    public string? StateValue { get; init; }
    public string? StateReason { get; init; }
    public DateTime? StateUpdatedTimestamp { get; init; }
    public bool? ActionsEnabled { get; init; }
    public List<string>? AlarmActions { get; init; }
    public Dictionary<string, string>? Dimensions { get; init; }
}

public record PutMetricAlarmRequestDto
{
    public required string AlarmName { get; init; }
    public string? AlarmDescription { get; init; }
    public required string MetricName { get; init; }
    public required string Namespace { get; init; }
    public required string Statistic { get; init; }
    public required int Period { get; init; }
    public required int EvaluationPeriods { get; init; }
    public required double Threshold { get; init; }
    public required string ComparisonOperator { get; init; }
    public Dictionary<string, string>? Dimensions { get; init; }
    public List<string>? AlarmActions { get; init; }
    public List<string>? OkActions { get; init; }
    public List<string>? InsufficientDataActions { get; init; }
    public string? Unit { get; init; }
    public bool? ActionsEnabled { get; init; }
    public int? DatapointsToAlarm { get; init; }
    public string? TreatMissingData { get; init; }
}

public record DeleteAlarmsRequestDto
{
    public required List<string> AlarmNames { get; init; }
}

public record AlarmNamesRequestDto
{
    public required List<string> AlarmNames { get; init; }
}

public record AlarmHistoryResponse
{
    public List<AlarmHistoryItemDto> AlarmHistoryItems { get; init; } = [];
    public string? NextToken { get; init; }
    public bool HasMore { get; init; }
    public int TotalReturned { get; init; }
}

public record AlarmHistoryItemDto
{
    public string? AlarmName { get; init; }
    public string? AlarmType { get; init; }
    public DateTime? Timestamp { get; init; }
    public string? HistoryItemType { get; init; }
    public string? HistorySummary { get; init; }
    public string? HistoryData { get; init; }
}

#endregion
