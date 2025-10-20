using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using AwsServer.CloudWatch;
using AwsServer.CloudWatch.Models;
using AwsServer.Controllers.Models;
using AwsServer.Controllers.Responses;
using Microsoft.AspNetCore.Mvc;
using QueryStatistics = AwsServer.Controllers.Models.QueryStatistics;

namespace AwsServer.Controllers;

/// <summary>
/// CloudWatch Logs API - simplified, pagination-first design.
/// All endpoints support AWS native pagination tokens.
/// For complex queries, use CloudWatch Logs Insights endpoints.
/// </summary>
[ApiController]
[Route("api/cloudwatch/logs")]
public class CloudWatchLogsController(
    CloudWatchLogsService service,
    ILogger<CloudWatchLogsController> logger)
    : ControllerBase
{
    #region Filtering Endpoints
    
    /// <summary>
    /// Filter log events with pagination.
    /// This is the primary log query endpoint.
    /// 
    /// Filter Pattern Examples:
    /// - "[ERROR]" - Find logs containing ERROR
    /// - "?ERROR ?Exception" - Find logs with ERROR OR Exception
    /// - "{ $.level = \"ERROR\" }" - JSON logs with level=ERROR
    /// - "[timestamp, request_id, level = ERROR*, ...]" - Field extraction
    /// 
    /// GET /api/cloudwatch/logs/filter?logGroupName=/aws/lambda/my-function&amp;filterPattern=[ERROR]&amp;limit=100
    /// </summary>
    [HttpGet("filter")]
    [ProducesResponseType(typeof(FilterLogsResponse), 200)]
    public async Task<IActionResult> FilterLogs(
        [FromQuery] string logGroupName,
        [FromQuery] string? filterPattern = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int limit = 100,
        [FromQuery] string? nextToken = null,
        [FromQuery] string? logStreamNames = null,
        [FromQuery] bool includePagination = false,
        [FromQuery] bool useQuickEstimate = true)
    {
        try
        {
            List<string>? streams = ParseCommaSeparated(logStreamNames);
            
            FilterLogEventsResponse response = await service.FilterLogsAsync(
                logGroupName, filterPattern, startTime, endTime,
                limit, nextToken, streams);
            
            // Calculate pagination metadata if requested
            Common.Models.PaginationMetadata? pagination = null;
            if (includePagination)
            {
                // Get count estimate (only on first page to avoid extra overhead)
                long? estimatedCount = null;
                string confidence = "Unknown";
                
                if (string.IsNullOrEmpty(nextToken))
                {
                    var estimate = await service.GetCountEstimateAsync(
                        logGroupName, filterPattern, startTime, endTime, useQuickEstimate);
                    estimatedCount = estimate.count;
                    confidence = estimate.confidence;
                }
                
                // Note: Page number is difficult to track with opaque tokens
                // For now, we'll assume page 1 if no token, otherwise page 2+
                int pageNumber = string.IsNullOrEmpty(nextToken) ? 1 : 2;
                
                pagination = service.CalculatePaginationMetadata(
                    response.Events.Count,
                    limit,
                    pageNumber,
                    !string.IsNullOrEmpty(response.NextToken),
                    estimatedCount,
                    confidence);
            }
            
            return Ok(new FilterLogsResponse
            {
                Events = response.Events.Select(e => new LogEventDto
                {
                    Timestamp = CloudWatchLogsService.FromUnixMilliseconds(e.Timestamp ?? 0),
                    Message = e.Message,
                    LogStreamName = e.LogStreamName,
                    EventId = e.EventId
                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken),
                SearchedLogStreams = response.SearchedLogStreams?.Count ?? 0,
                TotalEventsReturned = response.Events.Count,
                Pagination = pagination
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering logs for {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Quick filter for recent logs (last N minutes).
    /// Convenience endpoint that sets the time range automatically.
    /// 
    /// GET /api/cloudwatch/logs/recent?logGroupName=/aws/lambda/my-function&amp;minutes=30&amp;filterPattern=[ERROR]
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(FilterLogsResponse), 200)]
    public async Task<IActionResult> GetRecentLogs(
        [FromQuery] string logGroupName,
        [FromQuery] int minutes = 30,
        [FromQuery] string? filterPattern = null,
        [FromQuery] int limit = 100,
        [FromQuery] string? nextToken = null)
    {
        DateTime startTime = DateTime.UtcNow.AddMinutes(-minutes);
        return await FilterLogs(logGroupName, filterPattern, startTime, null, limit, nextToken);
    }
    
    /// <summary>
    /// Get logs from a specific log stream with pagination.
    /// Use this when you know the exact stream you want to query.
    /// 
    /// GET /api/cloudwatch/logs/stream?logGroupName=/aws/lambda/my-function&amp;logStreamName=2025/10/19/[$LATEST]abc123
    /// </summary>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(GetLogEventsResult), 200)]
    public async Task<IActionResult> GetLogEvents(
        [FromQuery] string logGroupName,
        [FromQuery] string logStreamName,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int limit = 100,
        [FromQuery] string? nextToken = null,
        [FromQuery] bool startFromHead = true)
    {
        try
        {
            GetLogEventsResponse response = await service.GetLogEventsAsync(
                logGroupName, logStreamName, startTime, endTime,
                limit, nextToken, startFromHead);
            
            return Ok(new GetLogEventsResult
            {
                Events = response.Events.Select(e => new LogEventDto
                {
                    Timestamp = e.Timestamp ?? DateTime.MinValue,
                    Message = e.Message,
                    LogStreamName = logStreamName
                }).ToList(),
                NextForwardToken = response.NextForwardToken,
                NextBackwardToken = response.NextBackwardToken,
                TotalEventsReturned = response.Events.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting log events from stream {LogStream}", logStreamName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    #endregion
    
    #region CloudWatch Logs Insights
    
    /// <summary>
    /// Run a CloudWatch Logs Insights query and wait for results.
    /// Use this for complex log analysis, aggregations, and statistics.
    /// 
    /// Query Examples:
    /// - "fields @timestamp, @message | filter @message like /ERROR/ | limit 20"
    /// - "stats count() by bin(5m) | sort count desc"
    /// - "parse @message '* * * * *' as time, status, method, path, latency | stats avg(latency) by path"
    /// 
    /// POST /api/cloudwatch/logs/insights/query
    /// Body: { "logGroupNames": ["/aws/lambda/fn1"], "queryString": "fields @timestamp, @message | limit 20", "startTime": "2025-10-19T00:00:00Z", "endTime": "2025-10-19T23:59:59Z" }
    /// </summary>
    [HttpPost("insights/query")]
    [ProducesResponseType(typeof(InsightsQueryResult), 200)]
    public async Task<IActionResult> RunInsightsQuery([FromBody] InsightsQueryRequest request)
    {
        try
        {
            GetQueryResultsResponse response = await service.RunInsightsQueryAsync(
                request.LogGroupNames,
                request.QueryString,
                request.StartTime,
                request.EndTime,
                request.TimeoutSeconds.HasValue ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value) : null);
            
            return Ok(new InsightsQueryResult
            {
                Status = response.Status.Value,
                Results = response.Results,
                Statistics = new QueryStatistics
                {
                    RecordsMatched = response.Statistics?.RecordsMatched ?? 0,
                    RecordsScanned = response.Statistics?.RecordsScanned ?? 0,
                    BytesScanned = response.Statistics?.BytesScanned ?? 0
                }
            });
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Insights query timed out");
            return StatusCode(504, new { error = "Query timed out", message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running Insights query");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Start a CloudWatch Logs Insights query asynchronously.
    /// Use this for long-running queries. Poll the results endpoint to get results.
    /// 
    /// POST /api/cloudwatch/logs/insights/start
    /// Body: { "logGroupNames": ["/aws/lambda/fn1"], "queryString": "...", "startTime": "...", "endTime": "..." }
    /// </summary>
    [HttpPost("insights/start")]
    [ProducesResponseType(typeof(StartQueryResult), 200)]
    public async Task<IActionResult> StartInsightsQuery([FromBody] InsightsQueryRequest request)
    {
        try
        {
            string queryId = await service.StartInsightsQueryAsync(
                request.LogGroupNames,
                request.QueryString,
                request.StartTime,
                request.EndTime);
            
            return Ok(new StartQueryResult
            {
                QueryId = queryId,
                Status = "Running"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting Insights query");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Get results from a previously started Insights query.
    /// Poll this endpoint until status is "Complete".
    /// 
    /// GET /api/cloudwatch/logs/insights/results/{queryId}
    /// </summary>
    [HttpGet("insights/results/{queryId}")]
    [ProducesResponseType(typeof(InsightsQueryResult), 200)]
    public async Task<IActionResult> GetInsightsResults(string queryId)
    {
        try
        {
            GetQueryResultsResponse response = await service.GetInsightsQueryResultsAsync(queryId);
            
            return Ok(new InsightsQueryResult
            {
                QueryId = queryId,
                Status = response.Status.Value,
                Results = response.Results,
                Statistics = new QueryStatistics
                {
                    RecordsMatched = response.Statistics?.RecordsMatched ?? 0,
                    RecordsScanned = response.Statistics?.RecordsScanned ?? 0,
                    BytesScanned = response.Statistics?.BytesScanned ?? 0
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Insights query results for {QueryId}", queryId);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Stop a running Insights query.
    /// 
    /// POST /api/cloudwatch/logs/insights/stop/{queryId}
    /// </summary>
    [HttpPost("insights/stop/{queryId}")]
    public async Task<IActionResult> StopInsightsQuery(string queryId)
    {
        try
        {
            await service.StopInsightsQueryAsync(queryId);
            return Ok(new { message = "Query stopped", queryId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping Insights query {QueryId}", queryId);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    #endregion
    
    #region Discovery Endpoints
    
    /// <summary>
    /// List log groups with pagination.
    /// 
    /// GET /api/cloudwatch/logs/log-groups?prefix=/aws/lambda&amp;limit=50
    /// </summary>
    [HttpGet("log-groups")]
    [ProducesResponseType(typeof(LogGroupsResponse), 200)]
    public async Task<IActionResult> ListLogGroups(
        [FromQuery] string? prefix = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? nextToken = null)
    {
        try
        {
            DescribeLogGroupsResponse response = await service.ListLogGroupsAsync(prefix, limit, nextToken);
            
            return Ok(new LogGroupsResponse
            {
                LogGroups = response.LogGroups.Select(lg => new LogGroupDto
                {
                    Name = lg.LogGroupName,
                    Arn = lg.Arn,
                    StoredBytes = lg.StoredBytes ?? 0,
                    RetentionInDays = lg.RetentionInDays,
                    CreationTime = lg.CreationTime ?? DateTime.MinValue
                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log groups");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// List log streams within a log group with pagination.
    /// 
    /// GET /api/cloudwatch/logs/streams?logGroupName=/aws/lambda/my-function&limit=50

    /// </summary>
    [HttpGet("streams")]

    [ProducesResponseType(typeof(LogStreamsResponse), 200)]
    public async Task<IActionResult> ListLogStreams(
        [FromQuery] string logGroupName,
        [FromQuery] string? prefix = null,
        [FromQuery] string? orderBy = "LastEventTime",
        [FromQuery] bool descending = true,
        [FromQuery] int limit = 50,
        [FromQuery] string? nextToken = null)
    {
        try
        {
            OrderBy? order = orderBy?.ToLower() switch
            {
                "logstreamname" => OrderBy.LogStreamName,
                "lasteventtime" => OrderBy.LastEventTime,
                _ => OrderBy.LastEventTime
            };
            
            DescribeLogStreamsResponse response = await service.ListLogStreamsAsync(
                logGroupName, prefix, order, descending, limit, nextToken);
            
            return Ok(new LogStreamsResponse
            {
                LogGroupName = logGroupName,
                Streams = response.LogStreams.Select(s => new LogStreamDto
                {
                    Name = s.LogStreamName,
                    Arn = s.Arn,
                    FirstEventTime = s.FirstEventTimestamp,
                    LastEventTime = s.LastEventTimestamp,
                    CreationTime = s.CreationTime ?? DateTime.MinValue,
                    StoredBytes = 0  // StoredBytes is deprecated by AWS and always returns 0

                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log streams for {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    #endregion
    
    #region Management Endpoints
    
    /// <summary>
    /// Create a new log group.
    /// 
    /// POST /api/cloudwatch/logs/log-groups
    /// Body: { "logGroupName": "/my/application/logs" }
    /// </summary>
    [HttpPost("log-groups")]
    public async Task<IActionResult> CreateLogGroup([FromBody] CreateLogGroupRequest request)
    {
        try
        {
            await service.CreateLogGroupAsync(request.LogGroupName);
            return Ok(new { message = "Log group created", logGroupName = request.LogGroupName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating log group {LogGroup}", request.LogGroupName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Delete a log group.
    /// 
    /// DELETE /api/cloudwatch/logs/log-groups/{logGroupName}
    /// </summary>
    [HttpDelete("log-groups/{**logGroupName}")]
    public async Task<IActionResult> DeleteLogGroup(string logGroupName)
    {
        try
        {
            await service.DeleteLogGroupAsync(logGroupName);
            return Ok(new { message = "Log group deleted", logGroupName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting log group {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Set the retention policy for a log group.
    /// 
    /// PUT /api/cloudwatch/logs/retention?logGroupName=/aws/lambda/my-function

    /// Body: { "retentionInDays": 7 }
    /// </summary>
    [HttpPut("retention")]

    public async Task<IActionResult> SetRetentionPolicy(
        [FromQuery] string logGroupName,
        [FromBody] SetRetentionRequest request)
    {
        try
        {
            await service.SetRetentionPolicyAsync(logGroupName, request.RetentionInDays);
            return Ok(new { message = "Retention policy updated", logGroupName, retentionInDays = request.RetentionInDays });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting retention for log group {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    #endregion
    

    #region Multi-Group and Convenience Endpoints
    
    /// <summary>
    /// Filter logs across multiple log groups simultaneously.
    /// Useful for cross-service troubleshooting and correlation.
    /// 
    /// GET /api/cloudwatch/logs/filter-multi?logGroupNames=group1,group2&filterPattern=[ERROR]&minutes=30
    /// </summary>
    [HttpGet("filter-multi")]
    [ProducesResponseType(typeof(MultiGroupLogsResponse), 200)]
    public async Task<IActionResult> FilterLogsMultiGroup(
        [FromQuery] string logGroupNames,
        [FromQuery] string? filterPattern = null,
        [FromQuery] int? minutes = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            List<string>? groups = ParseCommaSeparated(logGroupNames);
            if (groups == null || groups.Count == 0)
            {
                return BadRequest(new { error = "At least one log group name is required" });
            }

            if (minutes.HasValue)
            {
                startTime = DateTime.UtcNow.AddMinutes(-minutes.Value);
                endTime = null;
            }

            MultiGroupFilterResult result = await service.FilterLogsMultiGroupAsync(
                groups, filterPattern, startTime, endTime, limit);
            
            return Ok(new MultiGroupLogsResponse
            {
                LogGroupResults = result.LogGroupResults.Select(r => new LogGroupResultDto
                {
                    LogGroupName = r.LogGroupName,
                    Events = r.Events.Select(e => new LogEventDto
                    {
                        Timestamp = CloudWatchLogsService.FromUnixMilliseconds(e.Timestamp ?? 0),
                        Message = e.Message,
                        LogStreamName = e.LogStreamName,
                        EventId = e.EventId
                    }).ToList(),
                    Success = r.Success,
                    Error = r.Error,
                    QueryDurationMs = r.QueryDurationMs,
                    EventCount = r.EventCount
                }).ToList(),
                TotalEvents = result.TotalEvents,
                TotalDurationMs = result.TotalDurationMs,
                SuccessfulQueries = result.SuccessfulQueries,
                FailedQueries = result.FailedQueries
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering logs across multiple log groups");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Quick filter for recent logs across multiple log groups.
    /// 
    /// GET /api/cloudwatch/logs/recent-multi?logGroupNames=group1,group2&minutes=30&filterPattern=[ERROR]
    /// </summary>
    [HttpGet("recent-multi")]
    [ProducesResponseType(typeof(MultiGroupLogsResponse), 200)]
    public async Task<IActionResult> GetRecentLogsMultiGroup(
        [FromQuery] string logGroupNames,
        [FromQuery] int minutes = 30,
        [FromQuery] string? filterPattern = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            List<string>? groups = ParseCommaSeparated(logGroupNames);
            if (groups == null || groups.Count == 0)
            {
                return BadRequest(new { error = "At least one log group name is required" });
            }

            MultiGroupFilterResult result = await service.FilterRecentLogsMultiGroupAsync(
                groups, minutes, filterPattern, limit);
            
            return Ok(new MultiGroupLogsResponse
            {
                LogGroupResults = result.LogGroupResults.Select(r => new LogGroupResultDto
                {
                    LogGroupName = r.LogGroupName,
                    Events = r.Events.Select(e => new LogEventDto
                    {
                        Timestamp = CloudWatchLogsService.FromUnixMilliseconds(e.Timestamp ?? 0),
                        Message = e.Message,
                        LogStreamName = e.LogStreamName,
                        EventId = e.EventId
                    }).ToList(),
                    Success = r.Success,
                    Error = r.Error,
                    QueryDurationMs = r.QueryDurationMs,
                    EventCount = r.EventCount
                }).ToList(),
                TotalEvents = result.TotalEvents,
                TotalDurationMs = result.TotalDurationMs,
                SuccessfulQueries = result.SuccessfulQueries,
                FailedQueries = result.FailedQueries
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering recent logs across multiple log groups");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Search for error-level logs in a single log group.
    /// Automatically applies common error patterns (ERROR, Exception, FATAL, CRITICAL, Failed).
    /// 
    /// GET /api/cloudwatch/logs/errors?logGroupName=/aws/lambda/my-function&minutes=60
    /// </summary>
    [HttpGet("errors")]
    [ProducesResponseType(typeof(FilterLogsResponse), 200)]
    public async Task<IActionResult> GetErrorLogs(
        [FromQuery] string logGroupName,
        [FromQuery] int minutes = 60,
        [FromQuery] int limit = 100,
        [FromQuery] string? customErrorPattern = null)
    {
        try
        {
            FilterLogEventsResponse response = await service.FilterErrorLogsAsync(
                logGroupName, minutes, limit, customErrorPattern);
            
            return Ok(new FilterLogsResponse
            {
                Events = response.Events.Select(e => new LogEventDto
                {
                    Timestamp = CloudWatchLogsService.FromUnixMilliseconds(e.Timestamp ?? 0),
                    Message = e.Message,
                    LogStreamName = e.LogStreamName,
                    EventId = e.EventId
                }).ToList(),
                NextToken = response.NextToken,
                HasMore = !string.IsNullOrEmpty(response.NextToken),
                SearchedLogStreams = response.SearchedLogStreams?.Count ?? 0,
                TotalEventsReturned = response.Events.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering error logs for {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Search for error-level logs across multiple log groups.
    /// 
    /// GET /api/cloudwatch/logs/errors-multi?logGroupNames=group1,group2&minutes=60
    /// </summary>
    [HttpGet("errors-multi")]
    [ProducesResponseType(typeof(MultiGroupLogsResponse), 200)]
    public async Task<IActionResult> GetErrorLogsMultiGroup(
        [FromQuery] string logGroupNames,
        [FromQuery] int minutes = 60,
        [FromQuery] int limit = 100,
        [FromQuery] string? customErrorPattern = null)
    {
        try
        {
            List<string>? groups = ParseCommaSeparated(logGroupNames);
            if (groups == null || groups.Count == 0)
            {
                return BadRequest(new { error = "At least one log group name is required" });
            }

            MultiGroupFilterResult result = await service.FilterErrorLogsMultiGroupAsync(
                groups, minutes, limit, customErrorPattern);
            
            return Ok(new MultiGroupLogsResponse
            {
                LogGroupResults = result.LogGroupResults.Select(r => new LogGroupResultDto
                {
                    LogGroupName = r.LogGroupName,
                    Events = r.Events.Select(e => new LogEventDto
                    {
                        Timestamp = CloudWatchLogsService.FromUnixMilliseconds(e.Timestamp ?? 0),
                        Message = e.Message,
                        LogStreamName = e.LogStreamName,
                        EventId = e.EventId
                    }).ToList(),
                    Success = r.Success,
                    Error = r.Error,
                    QueryDurationMs = r.QueryDurationMs,
                    EventCount = r.EventCount
                }).ToList(),
                TotalEvents = result.TotalEvents,
                TotalDurationMs = result.TotalDurationMs,
                SuccessfulQueries = result.SuccessfulQueries,
                FailedQueries = result.FailedQueries
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering error logs across multiple log groups");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Search for a specific pattern across multiple log groups.
    /// Useful for finding trace IDs, specific error messages, etc.
    /// 
    /// GET /api/cloudwatch/logs/search-pattern?logGroupNames=group1,group2&pattern=SCRAM-SHA-1&minutes=60
    /// </summary>
    [HttpGet("search-pattern")]
    [ProducesResponseType(typeof(MultiGroupLogsResponse), 200)]
    public async Task<IActionResult> SearchPattern(
        [FromQuery] string logGroupNames,
        [FromQuery] string pattern,
        [FromQuery] int minutes = 60,
        [FromQuery] int limit = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return BadRequest(new { error = "Pattern is required" });
            }

            List<string>? groups = ParseCommaSeparated(logGroupNames);
            if (groups == null || groups.Count == 0)
            {
                return BadRequest(new { error = "At least one log group name is required" });
            }

            MultiGroupFilterResult result = await service.SearchPatternMultiGroupAsync(
                groups, pattern, minutes, limit);
            
            return Ok(new MultiGroupLogsResponse
            {
                LogGroupResults = result.LogGroupResults.Select(r => new LogGroupResultDto
                {
                    LogGroupName = r.LogGroupName,
                    Events = r.Events.Select(e => new LogEventDto
                    {
                        Timestamp = CloudWatchLogsService.FromUnixMilliseconds(e.Timestamp ?? 0),
                        Message = e.Message,
                        LogStreamName = e.LogStreamName,
                        EventId = e.EventId
                    }).ToList(),
                    Success = r.Success,
                    Error = r.Error,
                    QueryDurationMs = r.QueryDurationMs,
                    EventCount = r.EventCount
                }).ToList(),
                TotalEvents = result.TotalEvents,
                TotalDurationMs = result.TotalDurationMs,
                SuccessfulQueries = result.SuccessfulQueries,
                FailedQueries = result.FailedQueries
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching pattern across multiple log groups");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    /// <summary>
    /// Get log context around a specific timestamp in a log stream.
    /// Returns N lines before and after the specified timestamp for debugging.
    /// 
    /// GET /api/cloudwatch/logs/context?logGroupName=/aws/lambda/my-function&logStreamName=2024/10/19/stream&timestamp=1729372800000&contextLines=50
    /// </summary>
    [HttpGet("context")]
    [ProducesResponseType(typeof(LogContextResponse), 200)]
    public async Task<IActionResult> GetLogContext(
        [FromQuery] string logGroupName,
        [FromQuery] string logStreamName,
        [FromQuery] long timestamp,
        [FromQuery] int contextLines = 50)
    {
        try
        {
            LogContextResult result = await service.GetLogContextAsync(
                logGroupName, logStreamName, timestamp, contextLines);
            
            return Ok(new LogContextResponse
            {
                TargetTimestamp = result.TargetTimestamp,
                TargetEvent = result.TargetEvent != null ? new LogEventDto
                {
                    Timestamp = result.TargetEvent.Timestamp ?? DateTime.MinValue,
                    Message = result.TargetEvent.Message,
                    LogStreamName = logStreamName
                } : null,
                EventsBefore = result.EventsBefore,
                EventsAfter = result.EventsAfter,
                ContextEvents = result.ContextEvents.Select(e => new LogEventDto
                {
                    Timestamp = e.Timestamp ?? DateTime.MinValue,
                    Message = e.Message,
                    LogStreamName = logStreamName
                }).ToList(),
                TotalContextEvents = result.TotalContextEvents
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting log context for {LogGroup}/{LogStream}", logGroupName, logStreamName);
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
    
    #endregion
    

    #region Helper Methods
    
    private static List<string>? ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }
    
    #endregion
}

#region Request/Response Records

public record InsightsQueryRequest(
    List<string> LogGroupNames,
    string QueryString,
    DateTime StartTime,
    DateTime EndTime,
    int? TimeoutSeconds = null);

public record CreateLogGroupRequest(string LogGroupName);

public record SetRetentionRequest(int RetentionInDays);

#endregion