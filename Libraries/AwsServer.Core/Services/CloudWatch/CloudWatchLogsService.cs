using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using AwsServer.Core.Configuration;
using AwsServer.Core.Configuration.Models;
using AwsServer.Core.Services.CloudWatch.Models;
using Microsoft.Extensions.Logging;
using InvalidOperationException = System.InvalidOperationException;


namespace AwsServer.Core.Services.CloudWatch;

/// <summary>
/// Simplified CloudWatch Logs service focused on pagination and server-side filtering.
/// All complex queries should use CloudWatch Logs Insights.
/// </summary>
public class CloudWatchLogsService : IDisposable
{
    public bool IsInitialized => _initialized && _logsClient != null;
    
    private readonly ILogger<CloudWatchLogsService> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonCloudWatchLogsClient? _logsClient;
    private AwsConfiguration? _config;
    private bool _initialized;
    
    public CloudWatchLogsService(
        ILogger<CloudWatchLogsService> logger,
        AwsDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _ = Task.Run(AutoInitializeAsync);
    }
    
    /// <summary>
    /// Initialize CloudWatch Logs client with configuration
    /// </summary>
    public async Task<bool> InitializeAsync(AwsConfiguration config)
    {
        try
        {
            _config = config;
            
            var clientConfig = new AmazonCloudWatchLogsConfig
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps
            };
            
            if (!string.IsNullOrEmpty(config.ServiceUrl))
            {
                clientConfig.ServiceURL = config.ServiceUrl;
            }
            
            var credentialsProvider = new AwsCredentialsProvider(config);
            AWSCredentials? credentials = credentialsProvider.GetCredentials();
            
            _logsClient = credentials != null
                ? new AmazonCloudWatchLogsClient(credentials, clientConfig)
                : new AmazonCloudWatchLogsClient(clientConfig);
            
            // Test connection
            await _logsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest { Limit = 1 });
            
            _initialized = true;
            _logger.LogInformation("CloudWatch Logs client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CloudWatch Logs client");
            _logsClient?.Dispose();
            _logsClient = null;
            return false;
        }
    }
    
    #region Core Filtering Methods
    
    /// <summary>
    /// Filter log events with pagination support.
    /// This is the primary method - all log queries should use this or build on it.
    /// </summary>
    /// <param name="logGroupName">Name of the log group</param>
    /// <param name="filterPattern">CloudWatch filter pattern (e.g., "[ERROR]", "{ $.level = \"ERROR\" }")</param>
    /// <param name="startTime">Start time for the query</param>
    /// <param name="endTime">End time for the query</param>
    /// <param name="limit">Maximum number of events to return (1-10000)</param>
    /// <param name="nextToken">Pagination token from previous response</param>
    /// <param name="logStreamNames">Optional list of specific log streams to search</param>
    /// <returns>Filtered log events with pagination token</returns>
    public async Task<FilterLogEventsResponse> FilterLogsAsync(
        string logGroupName,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        string? nextToken = null,
        List<string>? logStreamNames = null)
    {
        EnsureInitialized();
        
        var request = new FilterLogEventsRequest
        {
            LogGroupName = logGroupName,
            Limit = Math.Clamp(limit, 1, 10000),
            NextToken = nextToken
        };
        
        if (!string.IsNullOrEmpty(filterPattern))
        {
            request.FilterPattern = filterPattern;
        }
        
        if (startTime.HasValue)
        {
            request.StartTime = ToUnixMilliseconds(startTime.Value);
        }
        
        if (endTime.HasValue)
        {
            request.EndTime = ToUnixMilliseconds(endTime.Value);
        }
        
        if (logStreamNames != null && logStreamNames.Count > 0)
        {
            request.LogStreamNames = logStreamNames;
        }
        
        return await _logsClient!.FilterLogEventsAsync(request);
    }
    
    /// <summary>
    /// Stream log events for processing large result sets.
    /// Automatically handles pagination internally.
    /// </summary>
    public async IAsyncEnumerable<FilteredLogEvent> StreamLogsAsync(
        string logGroupName,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int batchSize = 1000,
        int? maxEvents = null)
    {
        EnsureInitialized();
        
        string? nextToken = null;
        var eventCount = 0;
        
        do
        {
            FilterLogEventsResponse response = await FilterLogsAsync(
                logGroupName, filterPattern, startTime, endTime,
                batchSize, nextToken);
            
            foreach (FilteredLogEvent? evt in response.Events)
            {
                if (maxEvents.HasValue && eventCount >= maxEvents.Value)
                {
                    yield break;
                }
                
                yield return evt;
                eventCount++;
            }
            
            nextToken = response.NextToken;
            
        } while (!string.IsNullOrEmpty(nextToken));
    }
    
    #endregion
    
    #region Multi-Group and Convenience Methods
    
    /// <summary>
    /// Filter logs across multiple log groups simultaneously.
    /// This is useful for cross-service troubleshooting.
    /// </summary>
    /// <param name="logGroupNames">List of log group names to search</param>
    /// <param name="filterPattern">CloudWatch filter pattern</param>
    /// <param name="startTime">Start time for the query</param>
    /// <param name="endTime">End time for the query</param>
    /// <param name="limit">Maximum number of events to return per log group</param>
    /// <returns>Consolidated results from all log groups with timing metadata</returns>
    public async Task<MultiGroupFilterResult> FilterLogsMultiGroupAsync(
        List<string> logGroupNames,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100)
    {
        EnsureInitialized();
        
        DateTime startTimestamp = DateTime.UtcNow;
        IEnumerable<Task<LogGroupResult>> tasks = logGroupNames.Select(async logGroupName =>
        {
            try
            {
                DateTime groupStartTime = DateTime.UtcNow;
                FilterLogEventsResponse response = await FilterLogsAsync(
                    logGroupName, filterPattern, startTime, endTime, limit);
                TimeSpan duration = DateTime.UtcNow - groupStartTime;
                
                return new LogGroupResult
                {
                    LogGroupName = logGroupName,
                    Events = response.Events,
                    Success = true,
                    QueryDurationMs = (int)duration.TotalMilliseconds,
                    EventCount = response.Events.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error filtering logs for log group {LogGroup}", logGroupName);
                return new LogGroupResult
                {
                    LogGroupName = logGroupName,
                    Events = [],
                    Success = false,
                    Error = ex.Message,
                    EventCount = 0
                };
            }
        });
        
        LogGroupResult[] results = await Task.WhenAll(tasks);
        TimeSpan totalDuration = DateTime.UtcNow - startTimestamp;
        
        return new MultiGroupFilterResult
        {
            LogGroupResults = results.ToList(),
            TotalEvents = results.Sum(r => r.EventCount),
            TotalDurationMs = (int)totalDuration.TotalMilliseconds,
            SuccessfulQueries = results.Count(r => r.Success),
            FailedQueries = results.Count(r => !r.Success)
        };
    }
    
    /// <summary>
    /// Quick filter for recent logs across multiple log groups.
    /// Convenience endpoint that sets the time range automatically.
    /// </summary>
    public async Task<MultiGroupFilterResult> FilterRecentLogsMultiGroupAsync(
        List<string> logGroupNames,
        int minutes = 30,
        string? filterPattern = null,
        int limit = 100)
    {
        DateTime startTime = DateTime.UtcNow.AddMinutes(-minutes);
        return await FilterLogsMultiGroupAsync(logGroupNames, filterPattern, startTime, null, limit);
    }
    
    /// <summary>
    /// Search for error-level logs in a log group.
    /// Automatically applies common error patterns.
    /// </summary>
    /// <param name="logGroupName">Log group to search</param>
    /// <param name="minutes">How many minutes back to search</param>
    /// <param name="limit">Maximum number of events to return</param>
    /// <param name="customErrorPattern">Optional custom error pattern (overrides default)</param>
    public async Task<FilterLogEventsResponse> FilterErrorLogsAsync(
        string logGroupName,
        int minutes = 60,
        int limit = 100,
        string? customErrorPattern = null)
    {
        // Default error pattern matches common error indicators
        // CloudWatch filter pattern: space-separated terms are OR'd together
        string errorPattern = customErrorPattern ?? "ERROR Exception FATAL CRITICAL Failed \"level\":\"error\"";

        
        DateTime startTime = DateTime.UtcNow.AddMinutes(-minutes);
        return await FilterLogsAsync(logGroupName, errorPattern, startTime, null, limit);
    }
    
    /// <summary>
    /// Search for error-level logs across multiple log groups.
    /// </summary>
    public async Task<MultiGroupFilterResult> FilterErrorLogsMultiGroupAsync(
        List<string> logGroupNames,
        int minutes = 60,
        int limit = 100,
        string? customErrorPattern = null)
    {
        // Same pattern as single group
        string errorPattern = customErrorPattern ?? "ERROR Exception FATAL CRITICAL Failed \"level\":\"error\"";

        DateTime startTime = DateTime.UtcNow.AddMinutes(-minutes);
        return await FilterLogsMultiGroupAsync(logGroupNames, errorPattern, startTime, null, limit);
    }
    
    /// <summary>
    /// Search for a specific pattern across multiple log groups.
    /// Useful for finding specific error messages, trace IDs, etc.
    /// </summary>
    public async Task<MultiGroupFilterResult> SearchPatternMultiGroupAsync(
        List<string> logGroupNames,
        string searchPattern,
        int minutes = 60,
        int limit = 100)
    {
        DateTime startTime = DateTime.UtcNow.AddMinutes(-minutes);
        return await FilterLogsMultiGroupAsync(logGroupNames, searchPattern, startTime, null, limit);
    }
    
    /// <summary>
    /// Get log context around a specific timestamp in a log stream.
    /// Returns N lines before and after the specified timestamp.
    /// </summary>
    /// <param name="logGroupName">Log group name</param>
    /// <param name="logStreamName">Log stream name</param>
    /// <param name="timestamp">Target timestamp (Unix milliseconds)</param>
    /// <param name="contextLines">Number of lines to retrieve before and after (default: 50)</param>
    public async Task<LogContextResult> GetLogContextAsync(
        string logGroupName,
        string logStreamName,
        long timestamp,
        int contextLines = 50)
    {
        EnsureInitialized();
        
        DateTime targetTime = FromUnixMilliseconds(timestamp);
        
        // Get logs before the target time
        var beforeRequest = new GetLogEventsRequest
        {
            LogGroupName = logGroupName,
            LogStreamName = logStreamName,
            EndTime = targetTime,
            Limit = contextLines,
            StartFromHead = false // Read backwards from end time
        };
        
        // Get logs after the target time  
        var afterRequest = new GetLogEventsRequest
        {
            LogGroupName = logGroupName,
            LogStreamName = logStreamName,
            StartTime = targetTime,
            Limit = contextLines + 1, // +1 to include the target event
            StartFromHead = true
        };
        
        Task<GetLogEventsResponse>? beforeTask = _logsClient!.GetLogEventsAsync(beforeRequest);
        Task<GetLogEventsResponse>? afterTask = _logsClient!.GetLogEventsAsync(afterRequest);
        
        await Task.WhenAll(beforeTask, afterTask);
        
        List<OutputLogEvent>? beforeEvents = beforeTask.Result.Events;
        List<OutputLogEvent>? afterEvents = afterTask.Result.Events;
        
        // Find the target event
        OutputLogEvent? targetEvent = afterEvents.FirstOrDefault(e => e.Timestamp == DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime);
        
        // Combine events (before + target + after)
        var allEvents = new List<OutputLogEvent>();
        allEvents.AddRange(beforeEvents);
        if (targetEvent != null)
        {
            allEvents.Add(targetEvent);
            allEvents.AddRange(afterEvents.Where(e => e.Timestamp != DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime));
        }
        else
        {
            allEvents.AddRange(afterEvents);
        }
        
        // Sort by timestamp
        allEvents = allEvents.OrderBy(e => e.Timestamp).ToList();
        
        return new LogContextResult
        {
            TargetTimestamp = timestamp,
            TargetEvent = targetEvent,
            EventsBefore = beforeEvents.Count,
            EventsAfter = afterEvents.Count(e => e.Timestamp != DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime),
            ContextEvents = allEvents,
            TotalContextEvents = allEvents.Count
        };
    }
    
    #endregion

    
    
    #region Pagination Helpers
    
    /// <summary>
    /// Batch retrieve all logs matching filter, automatically paginating through all results.
    /// Use with caution - can return large amounts of data.
    /// </summary>
    /// <param name="logGroupName">Log group to query</param>
    /// <param name="filterPattern">Filter pattern</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="maxResults">Maximum total results to retrieve (default: 10000)</param>
    /// <param name="pageSize">Page size for each request (default: 1000)</param>
    public async Task<BatchLogsResult> BatchRetrieveLogsAsync(
        string logGroupName,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int maxResults = 10000,
        int pageSize = 1000)
    {
        EnsureInitialized();
        
        var allEvents = new List<FilteredLogEvent>();
        var pageCount = 0;
        var totalDuration = 0;
        string? nextToken = null;
        
        DateTime startTimestamp = DateTime.UtcNow;
        
        do
        {
            DateTime pageStart = DateTime.UtcNow;
            
            FilterLogEventsResponse response = await FilterLogsAsync(
                logGroupName, filterPattern, startTime, endTime,
                Math.Min(pageSize, maxResults - allEvents.Count), nextToken);
            
            var pageDuration = (int)(DateTime.UtcNow - pageStart).TotalMilliseconds;
            totalDuration += pageDuration;
            
            allEvents.AddRange(response.Events);
            nextToken = response.NextToken;
            pageCount++;
            
            _logger.LogDebug("Page {PageNumber}: Retrieved {EventCount} events in {Duration}ms", 
                pageCount, response.Events.Count, pageDuration);
            
            if (allEvents.Count >= maxResults)
            {
                _logger.LogInformation("Reached max results limit of {MaxResults}", maxResults);
                break;
            }
            
        } while (!string.IsNullOrEmpty(nextToken));
        
        return new BatchLogsResult
        {
            Events = allEvents,
            TotalEvents = allEvents.Count,
            PageCount = pageCount,
            TotalDurationMs = totalDuration,
            AverageDurationPerPageMs = pageCount > 0 ? totalDuration / pageCount : 0,
            HasMoreResults = !string.IsNullOrEmpty(nextToken),
            NextToken = nextToken
        };
    }
    
    /// <summary>
    /// Estimate total count of events matching a filter.
    /// Uses sampling and CloudWatch Logs Insights for estimation.
    /// </summary>
    public async Task<EventCountEstimate> EstimateEventCountAsync(
        string logGroupName,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        EnsureInitialized();
        
        // Sample first page
        FilterLogEventsResponse sampleResponse = await FilterLogsAsync(
            logGroupName, filterPattern, startTime, endTime, 1000);
        
        // If less than a full page, that's our count
        if (string.IsNullOrEmpty(sampleResponse.NextToken))
        {
            return new EventCountEstimate
            {
                EstimatedCount = sampleResponse.Events.Count,
                IsExact = true,
                SampleSize = sampleResponse.Events.Count,
                Confidence = "Exact"
            };
        }
        
        // Try using Insights for better estimate
        try
        {
            string queryString = string.IsNullOrEmpty(filterPattern)
                ? "stats count() as count"
                : $"filter @message like /{filterPattern}/ | stats count() as count";
            
            GetQueryResultsResponse insightsResponse = await RunInsightsQueryAsync(
                [logGroupName],
                queryString,
                startTime ?? DateTime.UtcNow.AddHours(-1),
                endTime ?? DateTime.UtcNow,
                TimeSpan.FromSeconds(30));
            
            if (insightsResponse.Results.Count > 0 && insightsResponse.Results[0].Count > 0)
            {
                ResultField? countField = insightsResponse.Results[0].FirstOrDefault(f => f.Field == "count");
                if (countField != null && long.TryParse(countField.Value, out long count))
                {
                    return new EventCountEstimate
                    {
                        EstimatedCount = count,
                        IsExact = true,
                        SampleSize = (int)count,
                        Confidence = "High (Insights)"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not use Insights for count estimation");
        }
        
        // Fallback to sample-based estimation
        return new EventCountEstimate
        {
            EstimatedCount = sampleResponse.Events.Count * 10, // Rough estimate
            IsExact = false,
            SampleSize = sampleResponse.Events.Count,
            Confidence = "Low (Sample-based)"
        };
    }
    
    #endregion
    
    #region Log Format Detection
    
    /// <summary>
    /// Detect and parse structured log formats (JSON, key-value pairs, etc.)
    /// </summary>
    public async Task<StructuredLogsResult> GetStructuredLogsAsync(
        string logGroupName,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        bool parseJson = true,
        bool parseKeyValue = true)
    {
        EnsureInitialized();
        
        FilterLogEventsResponse response = await FilterLogsAsync(
            logGroupName, filterPattern, startTime, endTime, limit);
        
        var structuredEvents = new List<StructuredLogEvent>();
        var formatStats = new Dictionary<string, int>
        {
            ["json"] = 0,
            ["key-value"] = 0,
            ["plain-text"] = 0,
            ["unknown"] = 0
        };
        
        foreach (FilteredLogEvent? evt in response.Events)
        {
            var structured = new StructuredLogEvent
            {
                Timestamp = FromUnixMilliseconds(evt.Timestamp ?? 0),
                LogStreamName = evt.LogStreamName,
                EventId = evt.EventId,
                RawMessage = evt.Message,
                Format = "unknown"
            };
            
            // Try JSON parsing
            if (parseJson && evt.Message.TrimStart().StartsWith("{"))
            {
                try
                {
                    JsonDocument jsonDoc = JsonDocument.Parse(evt.Message);
                    structured.ParsedData = new Dictionary<string, object?>();
                    
                    foreach (JsonProperty prop in jsonDoc.RootElement.EnumerateObject())
                    {
                        structured.ParsedData[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => prop.Value.ToString()
                        };
                    }
                    
                    structured.Format = "json";
                    formatStats["json"]++;
                }
                catch
                {
                    // Not valid JSON
                }
            }
            
            // Try key-value parsing (e.g., "key1=value1 key2=value2")
            if (structured.Format == "unknown" && parseKeyValue && evt.Message.Contains('='))
            {
                Dictionary<string, string> kvPairs = ParseKeyValuePairs(evt.Message);
                if (kvPairs.Count > 0)
                {
                    structured.ParsedData = kvPairs.ToDictionary(k => k.Key, k => (object?)k.Value);
                    structured.Format = "key-value";
                    formatStats["key-value"]++;
                }
            }
            
            // Default to plain text
            if (structured.Format == "unknown")
            {
                if (string.IsNullOrWhiteSpace(evt.Message))
                {
                    formatStats["unknown"]++;
                }
                else
                {
                    structured.Format = "plain-text";
                    formatStats["plain-text"]++;
                }
            }
            
            structuredEvents.Add(structured);
        }
        
        return new StructuredLogsResult
        {
            Events = structuredEvents,
            TotalEvents = structuredEvents.Count,
            FormatStatistics = formatStats,
            NextToken = response.NextToken,
            HasMore = !string.IsNullOrEmpty(response.NextToken)
        };
    }
    
    private static Dictionary<string, string> ParseKeyValuePairs(string message)
    {
        var result = new Dictionary<string, string>();
        
        // Simple key=value parser
        string[] parts = message.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string part in parts)
        {
            int kvIndex = part.IndexOf('=');
            if (kvIndex > 0 && kvIndex < part.Length - 1)
            {
                string key = part.Substring(0, kvIndex);
                string value = part.Substring(kvIndex + 1).Trim('"', '\'');
                result[key] = value;
            }
        }
        
        return result;
    }
    
    #endregion

    #region CloudWatch Logs Insights
    
    /// <summary>
    /// Start a CloudWatch Logs Insights query for complex log analysis.
    /// Use this for aggregations, statistics, pattern analysis, etc.
    /// </summary>
    /// <param name="logGroupNames">List of log groups to query</param>
    /// <param name="queryString">CloudWatch Logs Insights query (KQL-like syntax)</param>
    /// <param name="startTime">Query start time</param>
    /// <param name="endTime">Query end time</param>
    /// <returns>Query ID for polling results</returns>
    public async Task<string> StartInsightsQueryAsync(
        List<string> logGroupNames,
        string queryString,
        DateTime startTime,
        DateTime endTime)
    {
        EnsureInitialized();
        
        var request = new StartQueryRequest
        {
            LogGroupNames = logGroupNames,
            QueryString = queryString,
            StartTime = ToUnixSeconds(startTime),
            EndTime = ToUnixSeconds(endTime)
        };
        
        StartQueryResponse? response = await _logsClient!.StartQueryAsync(request);
        
        _logger.LogInformation("Started Insights query {QueryId} for {LogGroupCount} log groups",
            response.QueryId, logGroupNames.Count);
        
        return response.QueryId;
    }
    
    /// <summary>
    /// Get the results of a CloudWatch Logs Insights query
    /// </summary>
    public async Task<GetQueryResultsResponse> GetInsightsQueryResultsAsync(string queryId)
    {
        EnsureInitialized();
        return await _logsClient!.GetQueryResultsAsync(new GetQueryResultsRequest { QueryId = queryId });
    }
    
    /// <summary>
    /// Run a CloudWatch Logs Insights query and wait for results.
    /// Polls automatically until completion or timeout.
    /// </summary>
    /// <param name="logGroupNames">List of log groups to query</param>
    /// <param name="queryString">CloudWatch Logs Insights query</param>
    /// <param name="startTime">Query start time</param>
    /// <param name="endTime">Query end time</param>
    /// <param name="timeout">Maximum time to wait for results</param>
    /// <param name="pollInterval">How often to check for results</param>
    /// <returns>Query results</returns>
    public async Task<GetQueryResultsResponse> RunInsightsQueryAsync(
        List<string> logGroupNames,
        string queryString,
        DateTime startTime,
        DateTime endTime,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        string queryId = await StartInsightsQueryAsync(logGroupNames, queryString, startTime, endTime);
        
        DateTime deadline = DateTime.UtcNow.Add(timeout ?? TimeSpan.FromMinutes(5));
        TimeSpan interval = pollInterval ?? TimeSpan.FromSeconds(1);
        
        while (DateTime.UtcNow < deadline)
        {
            GetQueryResultsResponse response = await GetInsightsQueryResultsAsync(queryId);
            
            if (response.Status == QueryStatus.Complete)
            {
                _logger.LogInformation("Query {QueryId} completed. Scanned {BytesScanned} bytes, {RecordsMatched} records matched",
                    queryId, response.Statistics?.BytesScanned, response.Statistics?.RecordsMatched);
                return response;
            }
            
            if (response.Status == QueryStatus.Failed)
            {
                throw new InvalidOperationException($"Query {queryId} failed");
            }
            
            if (response.Status == QueryStatus.Cancelled)
            {
                throw new InvalidOperationException($"Query {queryId} was cancelled");
            }
            
            await Task.Delay(interval);
        }
        
        throw new TimeoutException($"Query {queryId} timed out after {timeout?.TotalSeconds ?? 300} seconds");
    }
    
    /// <summary>
    /// Stop a running CloudWatch Logs Insights query
    /// </summary>
    public async Task StopInsightsQueryAsync(string queryId)
    {
        EnsureInitialized();
        await _logsClient!.StopQueryAsync(new StopQueryRequest { QueryId = queryId });
        _logger.LogInformation("Stopped query {QueryId}", queryId);
    }
    
    #endregion
    
    #region Discovery Methods
    
    /// <summary>
    /// List log groups with pagination
    /// </summary>
    public async Task<DescribeLogGroupsResponse> ListLogGroupsAsync(
        string? logGroupNamePrefix = null,
        int limit = 50,
        string? nextToken = null)
    {
        EnsureInitialized();
        
        var request = new DescribeLogGroupsRequest
        {
            Limit = Math.Clamp(limit, 1, 50),
            NextToken = nextToken
        };
        
        if (!string.IsNullOrEmpty(logGroupNamePrefix))
        {
            request.LogGroupNamePrefix = logGroupNamePrefix;
        }
        
        return await _logsClient!.DescribeLogGroupsAsync(request);
    }
    
    /// <summary>
    /// List log streams within a log group with pagination
    /// </summary>
    public async Task<DescribeLogStreamsResponse> ListLogStreamsAsync(
        string logGroupName,
        string? logStreamNamePrefix = null,
        OrderBy? orderBy = null,
        bool descending = true,
        int limit = 50,
        string? nextToken = null)
    {
        EnsureInitialized();
        
        var request = new DescribeLogStreamsRequest
        {
            LogGroupName = logGroupName,
            OrderBy = orderBy ?? OrderBy.LastEventTime,
            Descending = descending,
            Limit = Math.Clamp(limit, 1, 50),
            NextToken = nextToken
        };
        
        if (!string.IsNullOrEmpty(logStreamNamePrefix))
        {
            request.LogStreamNamePrefix = logStreamNamePrefix;
        }
        
        return await _logsClient!.DescribeLogStreamsAsync(request);
    }
    
    /// <summary>
    /// Get log events from a specific log stream with pagination
    /// </summary>
    public async Task<GetLogEventsResponse> GetLogEventsAsync(
        string logGroupName,
        string logStreamName,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        string? nextToken = null,
        bool startFromHead = true)
    {
        EnsureInitialized();
        
        var request = new GetLogEventsRequest
        {
            LogGroupName = logGroupName,
            LogStreamName = logStreamName,
            Limit = Math.Clamp(limit, 1, 10000),
            StartFromHead = startFromHead,
            NextToken = nextToken
        };
        
        if (startTime.HasValue)
        {
            request.StartTime = startTime.Value;
        }
        
        if (endTime.HasValue)
        {
            request.EndTime = endTime.Value;
        }
        
        return await _logsClient!.GetLogEventsAsync(request);
    }
    
    #endregion
    
    #region Management Methods
    
    /// <summary>
    /// Create a new log group
    /// </summary>
    public async Task<CreateLogGroupResponse> CreateLogGroupAsync(string logGroupName)
    {
        EnsureInitialized();
        return await _logsClient!.CreateLogGroupAsync(new CreateLogGroupRequest
        {
            LogGroupName = logGroupName
        });
    }
    
    /// <summary>
    /// Delete a log group
    /// </summary>
    public async Task<DeleteLogGroupResponse> DeleteLogGroupAsync(string logGroupName)
    {
        EnsureInitialized();
        return await _logsClient!.DeleteLogGroupAsync(new DeleteLogGroupRequest
        {
            LogGroupName = logGroupName
        });
    }
    
    /// <summary>
    /// Set log group retention policy
    /// </summary>
    public async Task<PutRetentionPolicyResponse> SetRetentionPolicyAsync(
        string logGroupName,
        int retentionInDays)
    {
        EnsureInitialized();
        return await _logsClient!.PutRetentionPolicyAsync(new PutRetentionPolicyRequest
        {
            LogGroupName = logGroupName,
            RetentionInDays = retentionInDays
        });
    }
    
    #endregion
    
    #region Helper Methods
    
    private void EnsureInitialized()
    {
        if (!_initialized || _logsClient == null)
        {
            throw new InvalidOperationException(
                "CloudWatch Logs client is not initialized. Call Initialize first or wait for auto-initialization.");
        }
    }
    
    private async Task AutoInitializeAsync()
    {
        try
        {
            if (_discoveryService.AutoInitialize())
            {
                AccountInfo accountInfo = await _discoveryService.GetAccountInfoAsync();
                
                var config = new AwsConfiguration
                {
                    Region = accountInfo.InferredRegion,
                    ProfileName = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default"
                };
                
                await InitializeAsync(config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-initialize CloudWatch Logs service");
        }
    }
    
    private static long ToUnixMilliseconds(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeMilliseconds();
    }
    
    private static long ToUnixSeconds(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeSeconds();
    }
    
    public static DateTime FromUnixMilliseconds(long milliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
    }
    
    
    /// <summary>
    /// Calculate pagination metadata from current response state.
    /// </summary>
    /// <param name="currentPageSize">Number of items in current response</param>
    /// <param name="limit">Maximum items per page (limit parameter)</param>
    /// <param name="pageNumber">Current page number (1-based)</param>
    /// <param name="hasNextToken">Whether a next token exists</param>
    /// <param name="estimatedTotal">Optional estimated total count</param>
    /// <param name="confidence">Optional confidence level for estimate</param>
    /// <returns>Populated PaginationMetadata object</returns>
    public Common.Models.PaginationMetadata CalculatePaginationMetadata(
        int currentPageSize,
        int limit,
        int pageNumber,
        bool hasNextToken,
        long? estimatedTotal = null,
        string? confidence = null)
    {
        int? estimatedPages = null;
        if (estimatedTotal.HasValue && limit > 0)
        {
            estimatedPages = (int)Math.Ceiling((double)estimatedTotal.Value / limit);
        }
        
        bool isExact = confidence == "Exact" || confidence?.Contains("Exact") == true;
        
        string summary;
        int startItem = ((pageNumber - 1) * limit) + 1;
        int endItem = startItem + currentPageSize - 1;
        
        if (estimatedTotal.HasValue)
        {
            string totalDisplay = isExact ? $"{estimatedTotal}" : $"~{estimatedTotal}";
            summary = $"Showing results {startItem}-{endItem} of {totalDisplay}";
        }
        else
        {
            summary = $"Showing results {startItem}-{endItem}";
        }
        
        return new Common.Models.PaginationMetadata
        {
            CurrentPage = pageNumber,
            ItemsInPage = currentPageSize,
            ItemsPerPage = limit,
            EstimatedTotal = estimatedTotal,
            EstimatedPages = estimatedPages,
            IsExactCount = isExact,
            Summary = summary,
            Confidence = confidence,
            HasMore = hasNextToken
        };
    }
    
    /// <summary>
    /// Get an accurate count estimate for filtered logs.
    /// Attempts to use CloudWatch Insights for accurate counts when possible,
    /// falls back to sampling-based estimation.
    /// </summary>
    /// <param name="logGroupName">Log group to estimate</param>
    /// <param name="filterPattern">Filter pattern to apply</param>
    /// <param name="startTime">Start time for the query</param>
    /// <param name="endTime">End time for the query</param>
    /// <param name="useFastEstimate">If true, uses quick sampling instead of Insights</param>
    /// <returns>Tuple of (estimatedCount, confidence)</returns>
    public async Task<(long? count, string confidence)> GetCountEstimateAsync(
        string logGroupName,
        string? filterPattern,
        DateTime? startTime,
        DateTime? endTime,
        bool useFastEstimate = false)
    {
        try
        {
            if (useFastEstimate)
            {
                // Quick sample-based estimate
                FilterLogEventsResponse sampleResponse = await FilterLogsAsync(
                    logGroupName, filterPattern, startTime, endTime, 100);
                
                if (string.IsNullOrEmpty(sampleResponse.NextToken))
                {
                    // Less than one page - exact count
                    return (sampleResponse.Events.Count, "Exact");
                }
                
                // Rough estimate based on sample
                return (sampleResponse.Events.Count * 10, "Low (Quick sample)");
            }
            
            // Try using Insights for accurate count
            try
            {
                string queryString = string.IsNullOrEmpty(filterPattern)
                    ? "stats count() as count"
                    : $"filter @message like /{Regex.Escape(filterPattern)}/ | stats count() as count";
                
                GetQueryResultsResponse insightsResponse = await RunInsightsQueryAsync(
                    [logGroupName],
                    queryString,
                    startTime ?? DateTime.UtcNow.AddHours(-1),
                    endTime ?? DateTime.UtcNow,
                    TimeSpan.FromSeconds(30));
                
                if (insightsResponse.Results.Count > 0 && insightsResponse.Results[0].Count > 0)
                {
                    ResultField? countField = insightsResponse.Results[0].FirstOrDefault(f => f.Field == "count");
                    if (countField != null && long.TryParse(countField.Value, out long count))
                    {
                        return (count, "High (Insights)");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not use Insights for count estimation, falling back to sampling");
            }
            
            // Fallback to sample-based estimation
            FilterLogEventsResponse fallbackResponse = await FilterLogsAsync(
                logGroupName, filterPattern, startTime, endTime, 1000);
            
            if (string.IsNullOrEmpty(fallbackResponse.NextToken))
            {
                return (fallbackResponse.Events.Count, "Exact");
            }
            
            // Estimate based on larger sample
            return (fallbackResponse.Events.Count * 5, "Medium (Sample-based)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error estimating count for {LogGroup}", logGroupName);
            return (null, "Unknown");
        }
    }

    #endregion
    
    public void Dispose()
    {
        _logsClient?.Dispose();
    }
}