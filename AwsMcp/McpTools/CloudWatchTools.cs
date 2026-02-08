using System.ComponentModel;
using System.Text.Json;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using AwsServer.Core.Services.CloudWatch;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Models;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AwsMcp.McpTools;

/// <summary>
/// MCP tools for AWS CloudWatch operations
/// </summary>
[McpServerToolType]
public class CloudWatchTools(
    CloudWatchLogsService logsService,
    CloudWatchMetricsService metricsService,
    ILogger<CloudWatchTools> logger,
    OutputGuard outputGuard)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    #region Log Groups Management

    [McpServerTool, DisplayName("list_log_groups")]
    [Description("List CloudWatch log groups. See skills/aws/cloudwatch/list-log-groups.md only when using this tool")]
    public async Task<string> ListLogGroups(
        string? prefix = null,
        int limit = 50)
    {
        try
        {
            logger.LogDebug("Listing CloudWatch log groups with prefix {Prefix}", prefix);
            DescribeLogGroupsResponse response = await logsService.ListLogGroupsAsync(prefix, limit);

            return JsonSerializer.Serialize(new
            {
                success = true,
                logGroupCount = response.LogGroups.Count,
                logGroups = response.LogGroups.Select(lg => new
                {
                    name = lg.LogGroupName,
                    arn = lg.Arn,
                    creationTime = lg.CreationTime,
                    storedBytes = lg.StoredBytes,
                    retentionInDays = lg.RetentionInDays
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing CloudWatch log groups");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("create_log_group")]
    [Description("Create CloudWatch log group. See skills/aws/cloudwatch/create-log-group.md only when using this tool")]
    public async Task<string> CreateLogGroup(
        string logGroupName)
    {
        try
        {
            logger.LogDebug("Creating CloudWatch log group {LogGroupName}", logGroupName);
            await logsService.CreateLogGroupAsync(logGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Log group created successfully",
                logGroupName
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating CloudWatch log group {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("delete_log_group")]
    [Description("Delete CloudWatch log group. See skills/aws/cloudwatch/delete-log-group.md only when using this tool")]
    public async Task<string> DeleteLogGroup(
        string logGroupName)
    {
        try
        {
            logger.LogDebug("Deleting CloudWatch log group {LogGroupName}", logGroupName);
            await logsService.DeleteLogGroupAsync(logGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Log group deleted successfully",
                logGroupName
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting CloudWatch log group {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("set_retention_policy")]
    [Description("Set log group retention policy. See skills/aws/cloudwatch/set-retention.md only when using this tool")]
    public async Task<string> SetRetentionPolicy(
        string logGroupName,
        int retentionInDays)
    {
        try
        {
            logger.LogDebug("Setting retention policy for {LogGroupName} to {Days} days", logGroupName, retentionInDays);
            await logsService.SetRetentionPolicyAsync(logGroupName, retentionInDays);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Retention policy set successfully",
                logGroupName,
                retentionInDays
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting retention policy for {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    #endregion

    #region Log Streams

    [McpServerTool, DisplayName("list_log_streams")]
    [Description("List CloudWatch log streams. See skills/aws/cloudwatch/list-log-streams.md only when using this tool")]
    public async Task<string> ListLogStreams(
        string logGroupName,
        string? prefix = null,
        string? orderBy = null,
        bool descending = true,
        int limit = 50)
    {
        try
        {
            logger.LogDebug("Listing log streams for group {LogGroupName}", logGroupName);

            OrderBy? orderByEnum = orderBy?.ToUpperInvariant() switch
            {
                "LASTEVENTTIME" => OrderBy.LastEventTime,
                "LOGSTREAMNAME" => OrderBy.LogStreamName,
                _ => null
            };

            DescribeLogStreamsResponse response = await logsService.ListLogStreamsAsync(
                logGroupName, prefix, orderByEnum, descending, limit);

            return JsonSerializer.Serialize(new
            {
                success = true,
                logStreamCount = response.LogStreams.Count,
                logStreams = response.LogStreams.Select(ls => new
                {
                    name = ls.LogStreamName,
                    creationTime = ls.CreationTime,
                    firstEventTime = ls.FirstEventTimestamp,
                    lastEventTime = ls.LastEventTimestamp
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log streams for group {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    #endregion

    #region Log Events and Filtering

    [McpServerTool, DisplayName("get_log_events")]
    [Description("Get CloudWatch log events. See skills/aws/cloudwatch/get-log-events.md only when using this tool")]
    public async Task<string> GetLogEvents(
        string logGroupName,
        string logStreamName,
        string? startTime = null,
        string? endTime = null,
        int limit = 100)
    {
        try
        {
            logger.LogDebug("Getting log events from {LogGroupName}/{LogStreamName}", logGroupName, logStreamName);

            DateTime? start = null;
            DateTime? end = null;

            if (!string.IsNullOrEmpty(startTime))
            {
                if (long.TryParse(startTime, out long unixStart))
                    start = DateTimeOffset.FromUnixTimeMilliseconds(unixStart).UtcDateTime;
                else
                    start = DateTime.Parse(startTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }

            if (!string.IsNullOrEmpty(endTime))
            {
                if (long.TryParse(endTime, out long unixEnd))
                    end = DateTimeOffset.FromUnixTimeMilliseconds(unixEnd).UtcDateTime;
                else
                    end = DateTime.Parse(endTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }

            GetLogEventsResponse response = await logsService.GetLogEventsAsync(logGroupName, logStreamName, start, end, limit);

            string result = JsonSerializer.Serialize(new
            {
                success = true,
                eventCount = response.Events.Count,
                events = response.Events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    message = e.Message,
                    ingestionTime = e.IngestionTime
                })
            }, _jsonOptions);

            // Check response size - log events can contain large message content
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "get_log_events");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Log events query returned {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Reduce limit parameter (currently {limit}, try 50 or 25)\n" +
                    "  2. Narrow the time range using startTime/endTime\n" +
                    "  3. Use filter_logs with a specific pattern to reduce results\n" +
                    "  4. Query logs in smaller time windows",
                    new {
                        currentLimit = limit,
                        suggestedLimit = Math.Max(10, limit / 4),
                        logGroupName,
                        logStreamName,
                        eventCount = response.Events.Count
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting log events from {LogGroupName}/{LogStreamName}",
                logGroupName, logStreamName);
            return ex.ToErrorResponse(outputGuard, errorCode: "GET_LOG_EVENTS_FAILED");
        }
    }

    [McpServerTool, DisplayName("filter_logs")]
    [Description("Filter CloudWatch logs. See skills/aws/cloudwatch/filter-logs.md only when using this tool")]
    public async Task<string> FilterLogs(
        string logGroupName,
        string? filterPattern = null,
        string? startTime = null,
        string? endTime = null,
        int limit = 100)
    {
        try
        {
            logger.LogDebug("Filtering logs in {LogGroupName} with pattern {Pattern}", logGroupName, filterPattern);

            DateTime? start = ParseDateTime(startTime);
            DateTime? end = ParseDateTime(endTime);

            FilterLogEventsResponse response = await logsService.FilterLogsAsync(logGroupName, filterPattern, start, end, limit);

            string result = JsonSerializer.Serialize(new
            {
                success = true,
                eventCount = response.Events.Count,
                events = response.Events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    message = e.Message,
                    logStreamName = e.LogStreamName,
                    eventId = e.EventId
                }),
                nextToken = response.NextToken
            }, _jsonOptions);

            // Check response size - filtered logs can return many matching events
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "filter_logs");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Filtered log query returned {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Reduce limit parameter (currently {limit}, try 50 or 25)\n" +
                    "  2. Use a more specific filter pattern\n" +
                    "  3. Narrow the time range with startTime/endTime\n" +
                    "  4. Query logs in smaller time windows",
                    new {
                        currentLimit = limit,
                        suggestedLimit = Math.Max(10, limit / 4),
                        logGroupName,
                        filterPattern,
                        eventCount = response.Events.Count
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering logs in {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "FILTER_LOGS_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_recent_logs")]
    [Description("Get recent CloudWatch logs. See skills/aws/cloudwatch/recent-logs.md only when using this tool")]
    public async Task<string> GetRecentLogs(
        string logGroupName,
        int minutes = 30,
        string? filterPattern = null,
        int limit = 100)
    {
        try
        {
            logger.LogDebug("Getting recent logs from {LogGroupName} for last {Minutes} minutes", logGroupName, minutes);

            DateTime end = DateTime.UtcNow;
            DateTime start = end.AddMinutes(-minutes);

            FilterLogEventsResponse response = await logsService.FilterLogsAsync(logGroupName, filterPattern, start, end, limit);

            string result = JsonSerializer.Serialize(new
            {
                success = true,
                timeRange = new { start, end, minutes },
                eventCount = response.Events.Count,
                events = response.Events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    message = e.Message,
                    logStreamName = e.LogStreamName
                })
            }, _jsonOptions);

            // Check response size - recent logs can return many events
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "get_recent_logs");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Recent logs query returned {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Reduce limit parameter (currently {limit}, try 50 or 25)\n" +
                    "  2. Reduce time window (currently {minutes} minutes)\n" +
                    "  3. Add a filter pattern to reduce results\n" +
                    "  4. Use filter_logs with specific time range",
                    new {
                        currentLimit = limit,
                        suggestedLimit = Math.Max(10, limit / 4),
                        currentMinutes = minutes,
                        suggestedMinutes = Math.Max(5, minutes / 2),
                        logGroupName,
                        eventCount = response.Events.Count
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent logs from {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "GET_RECENT_LOGS_FAILED");
        }
    }

    [McpServerTool, DisplayName("filter_logs_multi")]
    [Description("Filter logs across multiple groups. See skills/aws/cloudwatch/filter-logs-multi.md only when using this tool")]
    public async Task<string> FilterLogsMultiGroup(
        string logGroupNames,
        string? filterPattern = null,
        int minutes = 30,
        int limit = 100)
    {
        try
        {
            List<string> groups = logGroupNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim()).ToList();

            logger.LogDebug("Filtering logs across {Count} groups", groups.Count);

            DateTime end = DateTime.UtcNow;
            DateTime start = end.AddMinutes(-minutes);

            var results = new List<object>();

            foreach (string group in groups)
            {
                try
                {
                    FilterLogEventsResponse response = await logsService.FilterLogsAsync(group, filterPattern, start, end, limit);
                    results.Add(new
                    {
                        logGroupName = group,
                        eventCount = response.Events.Count,
                        events = response.Events.Select(e => new
                        {
                            timestamp = e.Timestamp,
                            message = e.Message,
                            logStreamName = e.LogStreamName
                        })
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { logGroupName = group, error = ex.Message });
                }
            }

            string result = JsonSerializer.Serialize(new
            {
                success = true,
                groupCount = groups.Count,
                timeRange = new { start, end, minutes },
                results
            }, _jsonOptions);

            // Check response size - multi-group queries multiply results across log groups
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "filter_logs_multi");

            if (!sizeCheck.IsWithinLimit)
            {
                int totalEvents = results.Sum(r =>
                {
                    var obj = r as dynamic;
                    return obj?.eventCount ?? 0;
                });

                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Multi-group log query across {groups.Count} groups returned {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Query fewer log groups at once\n" +
                    "  2. Reduce limit parameter (currently {limit}, try 50 or 25)\n" +
                    "  3. Reduce time window (currently {minutes} minutes)\n" +
                    "  4. Add a more specific filter pattern\n" +
                    "  5. Query log groups individually instead of multi-group",
                    new {
                        groupCount = groups.Count,
                        currentLimit = limit,
                        suggestedLimit = Math.Max(10, limit / 4),
                        currentMinutes = minutes,
                        suggestedMinutes = Math.Max(5, minutes / 2),
                        totalEvents
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering logs across multiple groups");
            return ex.ToErrorResponse(outputGuard, errorCode: "FILTER_LOGS_MULTI_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_recent_logs_multi")]
    [Description("Get recent logs from multiple groups. See skills/aws/cloudwatch/recent-logs-multi.md only when using this tool")]
    public async Task<string> GetRecentLogsMultiGroup(
        string logGroupNames,
        int minutes = 30,
        string? filterPattern = null,
        int limit = 100)
    {
        // This is essentially the same as filter_logs_multi, just with emphasis on recency
        return await FilterLogsMultiGroup(logGroupNames, filterPattern, minutes, limit);
    }

    [McpServerTool, DisplayName("get_error_logs")]
    [Description("Get error logs from group. See skills/aws/cloudwatch/error-logs.md only when using this tool")]
    public async Task<string> GetErrorLogs(
        string logGroupName,
        int minutes = 60,
        int limit = 100)
    {
        try
        {
            logger.LogDebug("Getting error logs from {LogGroupName}", logGroupName);

            DateTime end = DateTime.UtcNow;
            DateTime start = end.AddMinutes(-minutes);

            // Common error patterns
            var errorPattern = "[ERROR] OR [FATAL] OR Exception OR Failed OR Failure";

            FilterLogEventsResponse response = await logsService.FilterLogsAsync(logGroupName, errorPattern, start, end, limit);

            return JsonSerializer.Serialize(new
            {
                success = true,
                timeRange = new { start, end, minutes },
                errorCount = response.Events.Count,
                errors = response.Events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    message = e.Message,
                    logStreamName = e.LogStreamName
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting error logs from {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_error_logs_multi")]
    [Description("Get error logs from multiple groups. See skills/aws/cloudwatch/error-logs-multi.md only when using this tool")]
    public async Task<string> GetErrorLogsMultiGroup(
        string logGroupNames,
        int minutes = 60,
        int limit = 100)
    {
        var errorPattern = "[ERROR] OR [FATAL] OR Exception OR Failed OR Failure";
        return await FilterLogsMultiGroup(logGroupNames, errorPattern, minutes, limit);
    }

    [McpServerTool, DisplayName("search_pattern")]
    [Description("Search for pattern in logs. See skills/aws/cloudwatch/search-pattern.md only when using this tool")]
    public async Task<string> SearchPattern(
        string logGroupNames,
        string pattern,
        int minutes = 60,
        int limit = 100)
    {
        return await FilterLogsMultiGroup(logGroupNames, pattern, minutes, limit);
    }

    [McpServerTool, DisplayName("get_log_context")]
    [Description("Get log context around timestamp. See skills/aws/cloudwatch/log-context.md only when using this tool")]
    public async Task<string> GetLogContext(
        string logGroupName,
        string logStreamName,
        long timestamp,
        int contextLines = 50)
    {
        try
        {
            logger.LogDebug("Getting log context for timestamp {Timestamp}", timestamp);

            DateTime targetTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
            DateTime startTime = targetTime.AddMinutes(-5);
            DateTime endTime = targetTime.AddMinutes(5);

            GetLogEventsResponse response = await logsService.GetLogEventsAsync(
                logGroupName, logStreamName, startTime, endTime, contextLines * 2);

            // Find the event closest to the timestamp
            List<OutputLogEvent> events = response.Events.OrderBy(e =>
                Math.Abs((e.Timestamp.HasValue ? new DateTimeOffset(e.Timestamp.Value).ToUnixTimeMilliseconds() : 0) - timestamp)).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                targetTimestamp = timestamp,
                contextLines,
                eventCount = events.Count,
                events = events.Select(e => new
                {
                    timestamp = e.Timestamp.HasValue ? new DateTimeOffset(e.Timestamp.Value).ToUnixTimeMilliseconds() : 0,
                    message = e.Message,
                    isTarget = (e.Timestamp.HasValue ? new DateTimeOffset(e.Timestamp.Value).ToUnixTimeMilliseconds() : 0) == timestamp
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting log context");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    #endregion

    #region CloudWatch Logs Insights

    [McpServerTool, DisplayName("run_insights_query")]
    [Description("Run CloudWatch Insights query. See skills/aws/cloudwatch/insights-query.md only when using this tool")]
    public async Task<string> RunInsightsQuery(
        string logGroupNames,
        string queryString,
        string startTime,
        string endTime)
    {
        try
        {
            List<string> groups = logGroupNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim()).ToList();

            logger.LogDebug("Running Insights query on {Count} groups", groups.Count);

            DateTime? start = ParseDateTime(startTime);
            DateTime? end = ParseDateTime(endTime);

            if (start == null || end == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Start and end times are required"
                }, _jsonOptions);
            }

            string queryId = await logsService.StartInsightsQueryAsync(groups, queryString, start.Value, end.Value);

            // Wait for query to complete (with timeout)
            var maxWait = 30; // seconds
            var waited = 0;

            while (waited < maxWait)
            {
                await Task.Delay(1000);
                waited++;

                GetQueryResultsResponse results = await logsService.GetInsightsQueryResultsAsync(queryId);
                if (results.Status == QueryStatus.Complete)
                {
                    string result = JsonSerializer.Serialize(new
                    {
                        success = true,
                        queryId,
                        status = "Complete",
                        statistics = results.Statistics,
                        results = results.Results
                    }, _jsonOptions);

                    // Check response size - Insights queries can return very large result sets
                    ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "run_insights_query");

                    if (!sizeCheck.IsWithinLimit)
                    {
                        return outputGuard.CreateOversizedErrorResponse(
                            sizeCheck,
                            $"CloudWatch Insights query returned {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                            "Try these workarounds:\n" +
                            "  1. Add LIMIT clause to query (e.g., | limit 100)\n" +
                            "  2. Use more selective filters in the query\n" +
                            "  3. Narrow the time range (startTime/endTime)\n" +
                            "  4. Use aggregation functions (stats, count) instead of raw results\n" +
                            "  5. Query fewer log groups at once",
                            new {
                                queryId,
                                groupCount = groups.Count,
                                resultCount = results.Results?.Count ?? 0,
                                suggestedLimit = 100
                            });
                    }

                    return sizeCheck.SerializedJson!;
                }
                else if (results.Status == QueryStatus.Failed || results.Status == QueryStatus.Cancelled)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        queryId,
                        status = results.Status.Value,
                        error = "Query failed or was cancelled"
                    }, _jsonOptions);
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                queryId,
                error = "Query timed out after 30 seconds"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running Insights query");
            return ex.ToErrorResponse(outputGuard, errorCode: "RUN_INSIGHTS_QUERY_FAILED");
        }
    }

    [McpServerTool, DisplayName("start_insights_query")]
    [Description("Start CloudWatch Insights query. See skills/aws/cloudwatch/insights-start.md only when using this tool")]
    public async Task<string> StartInsightsQuery(
        string logGroupNames,
        string queryString,
        string startTime,
        string endTime)
    {
        try
        {
            List<string> groups = logGroupNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim()).ToList();

            logger.LogDebug("Starting Insights query on {Count} groups", groups.Count);

            DateTime? start = ParseDateTime(startTime);
            DateTime? end = ParseDateTime(endTime);

            if (start == null || end == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Start and end times are required"
                }, _jsonOptions);
            }

            string queryId = await logsService.StartInsightsQueryAsync(groups, queryString, start.Value, end.Value);

            return JsonSerializer.Serialize(new
            {
                success = true,
                queryId,
                message = "Query started successfully"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting Insights query");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_insights_results")]
    [Description("Get CloudWatch Insights results. See skills/aws/cloudwatch/insights-results.md only when using this tool")]
    public async Task<string> GetInsightsResults(
        string queryId)
    {
        try
        {
            logger.LogDebug("Getting Insights results for query {QueryId}", queryId);

            GetQueryResultsResponse results = await logsService.GetInsightsQueryResultsAsync(queryId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                queryId,
                status = results.Status?.Value,
                statistics = results.Statistics,
                results = results.Results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Insights results for {QueryId}", queryId);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("stop_insights_query")]
    [Description("Stop CloudWatch Insights query. See skills/aws/cloudwatch/insights-stop.md only when using this tool")]
    public async Task<string> StopInsightsQuery(
        string queryId)
    {
        try
        {
            logger.LogDebug("Stopping Insights query {QueryId}", queryId);

            await logsService.StopInsightsQueryAsync(queryId);

            return JsonSerializer.Serialize(new
            {
                queryId,
                message = "Query stopped successfully"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping Insights query {QueryId}", queryId);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    #endregion

    #region CloudWatch Metrics

    [McpServerTool, DisplayName("put_metric_data")]
    [Description("Put metric data to CloudWatch. See skills/aws/cloudwatch/put-metric-data.md only when using this tool")]
    public async Task<string> PutMetricData(
        string namespaceName,
        string metricName,
        double value,
        string? unit = null,
        string? timestamp = null)
    {
        try
        {
            logger.LogDebug("Putting metric {MetricName} to namespace {Namespace}", metricName, namespaceName);

            DateTime? time = null;
            if (!string.IsNullOrEmpty(timestamp))
            {
                time = DateTime.Parse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }

            // Create MetricDatum list
            var metricData = new List<MetricDatum>
            {
                new MetricDatum
                {
                    MetricName = metricName,
                    Value = value,
                    Unit = unit != null ? new Amazon.CloudWatch.StandardUnit(unit) : Amazon.CloudWatch.StandardUnit.None,
                    Timestamp = time ?? DateTime.UtcNow
                }
            };

            PutMetricDataResponse response = await metricsService.PutMetricDataAsync(namespaceName, metricData);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Metric data published successfully",
                namespaceName,
                metricName,
                value,
                unit,
                timestamp = time
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting metric {MetricName} to namespace {Namespace}",
                metricName, namespaceName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("filter_logs_multi")]
    [Description("Filter logs from multiple groups. See skills/aws/cloudwatch/filter-logs-multi.md only when using this tool")]
    public async Task<string> FilterLogsMulti(
        List<string> logGroupNames,
        string? filterPattern = null,
        string? startTime = null,
        string? endTime = null,
        int limit = 100)
    {
        try
        {
            logger.LogDebug("Filtering logs from {Count} log groups", logGroupNames.Count);
            var results = new List<object>();

            foreach (string logGroupName in logGroupNames)
            {
                try
                {
                    DateTime? start = string.IsNullOrEmpty(startTime) ? null : DateTime.Parse(startTime);
                    DateTime? end = string.IsNullOrEmpty(endTime) ? null : DateTime.Parse(endTime);

                    FilterLogEventsResponse response = await logsService.FilterLogsAsync(
                        logGroupName, filterPattern, start, end, limit);

                    results.Add(new
                    {
                        logGroupName,
                        success = true,
                        eventCount = response.Events.Count,
                        events = response.Events.Select(e => new
                        {
                            timestamp = e.Timestamp ?? 0,
                            message = e.Message,
                            logStreamName = e.LogStreamName
                        })
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        logGroupName,
                        success = false,
                        error = ex.Message
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                groupCount = logGroupNames.Count,
                results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering logs from multiple groups");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_recent_logs_multi")]
    [Description("Get recent logs from multiple groups. See skills/aws/cloudwatch/recent-logs-multi.md only when using this tool")]
    public async Task<string> GetRecentLogsMulti(
        List<string> logGroupNames,
        int minutesBack = 60,
        int limit = 100)
    {
        try
        {
            logger.LogDebug("Getting recent logs from {Count} log groups", logGroupNames.Count);
            var results = new List<object>();
            DateTime startTime = DateTime.UtcNow.AddMinutes(-minutesBack);
            DateTime endTime = DateTime.UtcNow;

            foreach (string logGroupName in logGroupNames)
            {
                try
                {
                    FilterLogEventsResponse response = await logsService.FilterLogsAsync(
                        logGroupName, null, startTime, endTime, limit);

                    results.Add(new
                    {
                        logGroupName,
                        success = true,
                        eventCount = response.Events.Count,
                        events = response.Events.Select(e => new
                        {
                            timestamp = e.Timestamp ?? 0,
                            message = e.Message,
                            logStreamName = e.LogStreamName
                        })
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        logGroupName,
                        success = false,
                        error = ex.Message
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                groupCount = logGroupNames.Count,
                minutesBack,
                results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent logs from multiple groups");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_error_logs_multi")]
    [Description("Get error logs from multiple groups. See skills/aws/cloudwatch/error-logs-multi.md only when using this tool")]
    public async Task<string> GetErrorLogsMulti(
        List<string> logGroupNames,
        int minutesBack = 60,
        int limit = 100)
    {
        try
        {
            logger.LogDebug("Getting error logs from {Count} log groups", logGroupNames.Count);
            var results = new List<object>();
            DateTime startTime = DateTime.UtcNow.AddMinutes(-minutesBack);
            DateTime endTime = DateTime.UtcNow;
            var filterPattern = "?ERROR ?EXCEPTION ?FAIL ?FATAL ?CRITICAL";

            foreach (string logGroupName in logGroupNames)
            {
                try
                {
                    FilterLogEventsResponse response = await logsService.FilterLogsAsync(
                        logGroupName, filterPattern, startTime, endTime, limit);

                    results.Add(new
                    {
                        logGroupName,
                        success = true,
                        eventCount = response.Events.Count,
                        events = response.Events.Select(e => new
                        {
                            timestamp = e.Timestamp ?? 0,
                            message = e.Message,
                            logStreamName = e.LogStreamName
                        })
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        logGroupName,
                        success = false,
                        error = ex.Message
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                groupCount = logGroupNames.Count,
                minutesBack,
                filterPattern,
                results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting error logs from multiple groups");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("search_log_pattern")]
    [Description("Search for pattern in logs. See skills/aws/cloudwatch/search-pattern.md only when using this tool")]
    public async Task<string> SearchLogPattern(
        string logGroupName,
        string searchPattern,
        int minutesBack = 60,
        int limit = 100,
        bool caseSensitive = false)
    {
        try
        {
            logger.LogDebug("Searching for pattern '{Pattern}' in {LogGroupName}", searchPattern, logGroupName);
            DateTime startTime = DateTime.UtcNow.AddMinutes(-minutesBack);
            DateTime endTime = DateTime.UtcNow;

            FilterLogEventsResponse response = await logsService.FilterLogsAsync(
                logGroupName, searchPattern, startTime, endTime, limit);

            return JsonSerializer.Serialize(new
            {
                success = true,
                logGroupName,
                searchPattern,
                caseSensitive,
                minutesBack,
                eventCount = response.Events.Count,
                searchedLogStreams = response.SearchedLogStreams?.Count ?? 0,
                events = response.Events.Select(e => new
                {
                    timestamp = e.Timestamp ?? 0,
                    message = e.Message,
                    logStreamName = e.LogStreamName
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching for pattern in {LogGroupName}", logGroupName);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("list_metric_namespaces")]
    [Description("List CloudWatch metric namespaces. See skills/aws/cloudwatch/list-namespaces.md only when using this tool")]
    public async Task<string> ListMetricNamespaces()
    {
        try
        {
            logger.LogDebug("Listing CloudWatch metric namespaces");
            ListMetricsResponse response = await metricsService.ListMetricsAsync();

            List<string> namespaces = response.Metrics
                .Select(m => m.Namespace)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                namespaceCount = namespaces.Count,
                namespaces
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing metric namespaces");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("delete_metric_alarm")]
    [Description("Delete CloudWatch metric alarm. See skills/aws/cloudwatch/delete-alarm.md only when using this tool")]
    public async Task<string> DeleteMetricAlarm(List<string> alarmNames)
    {
        try
        {
            logger.LogDebug("Deleting {Count} metric alarms", alarmNames.Count);
            DeleteAlarmsResponse response = await metricsService.DeleteAlarmsAsync(alarmNames);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Alarms deleted successfully",
                deletedAlarms = alarmNames
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting metric alarms");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("enable_metric_alarms")]
    [Description("Enable CloudWatch metric alarms. See skills/aws/cloudwatch/enable-alarms.md only when using this tool")]
    public async Task<string> EnableMetricAlarms(List<string> alarmNames)
    {
        try
        {
            logger.LogDebug("Enabling {Count} metric alarms", alarmNames.Count);
            EnableAlarmActionsResponse response = await metricsService.EnableAlarmActionsAsync(alarmNames);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Alarms enabled successfully",
                enabledAlarms = alarmNames
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enabling metric alarms");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("disable_metric_alarms")]
    [Description("Disable CloudWatch metric alarms. See skills/aws/cloudwatch/disable-alarms.md only when using this tool")]
    public async Task<string> DisableMetricAlarms(List<string> alarmNames)
    {
        try
        {
            logger.LogDebug("Disabling {Count} metric alarms", alarmNames.Count);
            DisableAlarmActionsResponse response = await metricsService.DisableAlarmActionsAsync(alarmNames);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Alarms disabled successfully",
                disabledAlarms = alarmNames
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disabling metric alarms");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_alarm_history")]
    [Description("Get CloudWatch alarm history. See skills/aws/cloudwatch/alarm-history.md only when using this tool")]
    public async Task<string> GetAlarmHistory(
        string? alarmName = null,
        List<string>? alarmTypes = null,
        string? historyItemType = null,
        string? startDate = null,
        string? endDate = null,
        int maxRecords = 100)
    {
        try
        {
            logger.LogDebug("Getting alarm history");

            DateTime? start = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? end = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate);

            DescribeAlarmHistoryResponse response = await metricsService.DescribeAlarmHistoryAsync(
                alarmName, alarmTypes, historyItemType, start, end, maxRecords);

            return JsonSerializer.Serialize(new
            {
                success = true,
                recordCount = response.AlarmHistoryItems.Count,
                history = response.AlarmHistoryItems.Select(h => new
                {
                    alarmName = h.AlarmName,
                    alarmType = h.AlarmType?.Value,
                    timestamp = h.Timestamp,
                    historyItemType = h.HistoryItemType?.Value,
                    historySummary = h.HistorySummary,
                    historyData = h.HistoryData
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting alarm history");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    #endregion

    #region Helper Methods

    private static DateTime? ParseDateTime(string? dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString))
            return null;

        return long.TryParse(dateTimeString, out long unixTime)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixTime).UtcDateTime
            : DateTime.Parse(dateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    #endregion
}