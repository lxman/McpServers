using System.Text.RegularExpressions;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using AwsServer.Controllers.Models;
using AwsServer.Controllers.Requests;
using AwsServer.Controllers.Responses;
using Microsoft.AspNetCore.Mvc;
using LogEvent = AwsServer.Controllers.Models.LogEvent;

namespace AwsServer.Controllers;

/// <summary>
/// Enhanced CloudWatch Logs Controller with improved log access patterns
/// </summary>
[ApiController]
[Route("api/cloudwatch")]
public class EnhancedCloudWatchController(
    IAmazonCloudWatchLogs cloudWatchClient,
    ILogger<EnhancedCloudWatchController> logger)
    : ControllerBase
{
    /// <summary>
    /// Get recent events from a log group
    /// GET /api/cloudwatch/recent?logGroupName=/application/TransactionProcessor&minutes=30&limit=100&level=ERROR
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentEvents(
        [FromQuery] string logGroupName,
        [FromQuery] int minutes = 30,
        [FromQuery] int limit = 100,
        [FromQuery] string? level = null)
    {
        try
        {
            var startTime = DateTime.UtcNow.AddMinutes(-minutes);
            var startTimeMillis = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();

            // Build filter pattern based on level
            var filterPattern = level switch
            {
                "ERROR" => "?ERROR ?Exception ?exception ?error ?fail",
                "WARN" => "?WARN ?WARNING ?warn ?warning",
                "INFO" => "?INFO ?info",
                _ => "" // All logs
            };

            var request = new FilterLogEventsRequest
            {
                LogGroupName = logGroupName,
                StartTime = startTimeMillis,
                FilterPattern = filterPattern,
                Limit = limit
            };

            var response = await cloudWatchClient.FilterLogEventsAsync(request);

            return Ok(new
            {
                logGroupName,
                timeRangeMinutes = minutes,
                level = level ?? "ALL",
                eventCount = response.Events.Count,
                events = response.Events.Select(e => new
                {
                    timestamp = DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp ?? 0).UtcDateTime,
                    message = e.Message,
                    logStreamName = e.LogStreamName
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent events from {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Search across multiple log groups
    /// POST /api/cloudwatch/search
    /// Body: { "searchText": "mongodb", "minutes": 60, "maxLogGroups": 5 }
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> SearchAllLogGroups([FromBody] SearchRequest request)
    {
        try
        {
            var startTime = DateTime.UtcNow.AddMinutes(-request.Minutes);
            var startTimeMillis = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();

            // Get all log groups
            var logGroupsRequest = new DescribeLogGroupsRequest
            {
                Limit = 50
            };

            var logGroupsResponse = await cloudWatchClient.DescribeLogGroupsAsync(logGroupsRequest);
            var results = new List<SearchResponse>();

            // Search in each log group (limited to maxLogGroups)
            var logGroupsToSearch = logGroupsResponse.LogGroups
                .Take(request.MaxLogGroups)
                .ToList();

            foreach (var logGroup in logGroupsToSearch)
            {
                try
                {
                    var filterRequest = new FilterLogEventsRequest
                    {
                        LogGroupName = logGroup.LogGroupName,
                        StartTime = startTimeMillis,
                        FilterPattern = request.SearchText,
                        Limit = 20 // Limit per log group
                    };

                    var filterResponse = await cloudWatchClient.FilterLogEventsAsync(filterRequest);

                    if (filterResponse.Events.Count != 0)
                    {
                        results.Add(new SearchResponse
                        {
                            LogGroupName = logGroup.LogGroupName,
                            EventCount = filterResponse.Events.Count,
                            Events = filterResponse.Events.Select(e => new LogEvent
                            {
                                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp ?? 0).UtcDateTime,
                                Message = e.Message,
                                LogStreamName = e.LogStreamName
                            }).ToList()
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error searching log group {LogGroup}", logGroup.LogGroupName);
                    // Continue searching other log groups
                }
            }

            return Ok(new
            {
                searchText = request.SearchText,
                timeRangeMinutes = request.Minutes,
                logGroupsSearched = logGroupsToSearch.Count,
                resultsFound = results.Count,
                results = results.OrderByDescending(r => r.EventCount).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching log groups");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent errors (common pattern)
    /// GET /api/cloudwatch/errors?logGroupName=/application/TransactionProcessor&minutes=30
    /// </summary>
    [HttpGet("errors")]
    public async Task<IActionResult> GetRecentErrors(
        [FromQuery] string logGroupName,
        [FromQuery] int minutes = 30,
        [FromQuery] int limit = 50)
    {
        try
        {
            var startTime = DateTime.UtcNow.AddMinutes(-minutes);
            var startTimeMillis = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();

            var request = new FilterLogEventsRequest
            {
                LogGroupName = logGroupName,
                StartTime = startTimeMillis,
                FilterPattern = "?ERROR ?Exception ?exception ?fail",
                Limit = limit
            };

            var response = await cloudWatchClient.FilterLogEventsAsync(request);

            var errors = response.Events.Select(ParseError).ToList();

            return Ok(new
            {
                logGroupName,
                timeRangeMinutes = minutes,
                errorCount = errors.Count,
                errors
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent errors from {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Tail logs (get most recent entries)
    /// GET /api/cloudwatch/tail?logGroupName=/application/TransactionProcessor&lines=100
    /// </summary>
    [HttpGet("tail")]
    public async Task<IActionResult> TailLogs(
        [FromQuery] string logGroupName,
        [FromQuery] int lines = 100)
    {
        try
        {
            // Get logs from the last 5 minutes
            var startTime = DateTime.UtcNow.AddMinutes(-5);
            var startTimeMillis = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();

            var request = new FilterLogEventsRequest
            {
                LogGroupName = logGroupName,
                StartTime = startTimeMillis,
                Limit = lines
            };

            var response = await cloudWatchClient.FilterLogEventsAsync(request);

            return Ok(new
            {
                logGroupName,
                lineCount = response.Events.Count,
                lines = response.Events
                    .OrderByDescending(e => e.Timestamp)
                    .Select(e => new
                    {
                        timestamp = DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp ?? 0).UtcDateTime,
                        message = e.Message,
                        logStreamName = e.LogStreamName
                    })
                    .ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error tailing logs from {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Search/discover log groups by pattern
    /// GET /api/cloudwatch/log-groups/search?pattern=*service*
    /// </summary>
    [HttpGet("log-groups/search")]
    public async Task<IActionResult> SearchLogGroups([FromQuery] string pattern)
    {
        try
        {
            var request = new DescribeLogGroupsRequest();
            var allLogGroups = new List<LogGroup>();
            string? nextToken = null;

            // Get all log groups (with pagination)
            do
            {
                request.NextToken = nextToken;
                var response = await cloudWatchClient.DescribeLogGroupsAsync(request);
                allLogGroups.AddRange(response.LogGroups);
                nextToken = response.NextToken;
            }
            while (nextToken != null);

            // Filter by pattern (simple wildcard matching)
            var searchPattern = pattern.Replace("*", ".*").Replace("?", ".");
            var regex = new Regex(searchPattern, 
                RegexOptions.IgnoreCase);

            var matchingGroups = allLogGroups
                .Where(lg => regex.IsMatch(lg.LogGroupName))
                .Select(lg => new
                {
                    logGroupName = lg.LogGroupName,
                    creationTime = lg.CreationTime,
                    storedBytes = lg.StoredBytes,
                    retentionInDays = lg.RetentionInDays
                })
                .OrderBy(lg => lg.logGroupName)
                .ToList();

            return Ok(new
            {
                pattern,
                totalLogGroups = allLogGroups.Count,
                matchingGroups = matchingGroups.Count,
                results = matchingGroups
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching log groups with pattern {Pattern}", pattern);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get log streams with fixed URL encoding
    /// GET /api/cloudwatch/log-streams?logGroupName=/application/TransactionProcessor
    /// </summary>
    [HttpGet("log-streams")]
    public async Task<IActionResult> GetLogStreams(
        [FromQuery] string logGroupName,
        [FromQuery] int limit = 50)
    {
        try
        {
            var request = new DescribeLogStreamsRequest
            {
                LogGroupName = logGroupName,
                OrderBy = OrderBy.LastEventTime,
                Descending = true,
                Limit = limit
            };

            var response = await cloudWatchClient.DescribeLogStreamsAsync(request);

            return Ok(new
            {
                logGroupName,
                streamCount = response.LogStreams.Count,
                streams = response.LogStreams.Select(s => new
                {
                    logStreamName = s.LogStreamName,
                    creationTime = s.CreationTime,
                    lastEventTime = s.LastEventTimestamp
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting log streams for {LogGroup}", logGroupName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Helper method to parse error information from log events
    /// </summary>
    private static ErrorInfo ParseError(FilteredLogEvent logEvent)
    {
        var message = logEvent.Message;
        var error = new ErrorInfo
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(logEvent.Timestamp ?? 0).UtcDateTime,
            LogStreamName = logEvent.LogStreamName,
            Message = message
        };

        // Try to extract exception type and message
        if (message.Contains("Exception:") || message.Contains("exception:"))
        {
            var lines = message.Split('\n', '\r');
            foreach (var line in lines)
            {
                if (line.Contains("Exception:") || line.Contains("exception:"))
                {
                    var parts = line.Split([':', ' '], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        error.ExceptionType = parts[0].Replace("Exception", "").Trim();
                    }
                }
            }
        }

        // Extract error level
        if (message.Contains("ERROR") || message.Contains("error"))
            error.Level = "ERROR";
        else if (message.Contains("WARN") || message.Contains("warn"))
            error.Level = "WARN";
        else if (message.Contains("FAIL") || message.Contains("fail"))
            error.Level = "FAIL";

        return error;
    }
}