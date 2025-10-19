using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using AwsServer.Configuration;
using AwsServer.Configuration.Models;
using InvalidOperationException = System.InvalidOperationException;

namespace AwsServer.CloudWatch;

/// <summary>
/// Simplified CloudWatch Logs service focused on pagination and server-side filtering.
/// All complex queries should use CloudWatch Logs Insights.
/// </summary>
public class CloudWatchLogsService : IDisposable
{
    private readonly ILogger<CloudWatchLogsService> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonCloudWatchLogsClient? _logsClient;
    private AwsConfiguration? _config;
    private bool _initialized;
    
    public bool IsInitialized => _initialized && _logsClient != null;
    
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
    
    #endregion
    
    public void Dispose()
    {
        _logsClient?.Dispose();
    }
}