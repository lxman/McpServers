using System.Diagnostics;
using System.Text.RegularExpressions;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using AwsServer.CloudWatch.Models;
using AwsServer.Configuration;
using InvalidOperationException = Amazon.CloudWatchLogs.Model.InvalidOperationException;
using Metric = Amazon.CloudWatch.Model.Metric;

namespace AwsServer.CloudWatch;

/// <summary>
/// Service for CloudWatch operations (metrics and logs)
/// </summary>
public class CloudWatchService
{
    private readonly ILogger<CloudWatchService> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonCloudWatchClient? _cloudWatchClient;
    private AmazonCloudWatchLogsClient? _logsClient;
    private AwsConfiguration? _config;
    private bool _cloudWatchInitialized;
    
    /// <summary>
    /// Check if metrics client is available
    /// </summary>
    public bool IsMetricsClientAvailable => _cloudWatchClient != null;

    /// <summary>
    /// Check if logs client is available
    /// </summary>
    public bool IsLogsClientAvailable => _logsClient != null;
    
    public CloudWatchService(
        ILogger<CloudWatchService> logger,
        AwsDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;

        _ = Task.Run(AutoInitializeAsync);
    }
    
    /// <summary>
    /// Initialize CloudWatch clients with configuration
    /// </summary>
    /// <param name="config">AWS configuration</param>
    /// <param name="testConnection">Whether to test connection during initialization (default: true)</param>
    /// <param name="testMetricsOnly">If testing, only test metrics client (default: false)</param>
    /// <param name="testLogsOnly">If testing, only test logs client (default: false)</param>
    public async Task<bool> InitializeAsync(AwsConfiguration config, bool testConnection = true, bool testMetricsOnly = false, bool testLogsOnly = false)
    {
        try
        {
            _config = config;
            
            var clientConfig = new AmazonCloudWatchConfig
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps
            };
            
            var logsConfig = new AmazonCloudWatchLogsConfig
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps
            };
            
            // Set custom endpoint if provided (for LocalStack, etc.)
            if (!string.IsNullOrEmpty(config.ServiceUrl))
            {
                clientConfig.ServiceURL = config.ServiceUrl;
                logsConfig.ServiceURL = config.ServiceUrl;
            }
            
            var credentialsProvider = new AwsCredentialsProvider(config);
            var credentials = credentialsProvider.GetCredentials();
            
            if (credentials != null)
            {
                _cloudWatchClient = new AmazonCloudWatchClient(credentials, clientConfig);
                _logsClient = new AmazonCloudWatchLogsClient(credentials, logsConfig);
            }
            else
            {
                _cloudWatchClient = new AmazonCloudWatchClient(clientConfig);
                _logsClient = new AmazonCloudWatchLogsClient(logsConfig);
            }
            
            // Optional connection testing with granular control
            if (testConnection)
            {
                var testResults = new List<string>();
                
                // Test metrics client if not logs-only
                if (!testLogsOnly)
                {
                    try
                    {
                        await _cloudWatchClient.ListMetricsAsync(new ListMetricsRequest());
                        testResults.Add("Metrics client: OK");
                        _logger.LogInformation("CloudWatch Metrics client connection test successful");
                    }
                    catch (Exception ex)
                    {
                        testResults.Add($"Metrics client: Failed - {ex.Message}");
                        _logger.LogWarning(ex, "CloudWatch Metrics client connection test failed");
                        
                        if (testMetricsOnly)
                        {
                            // If we're only testing metrics and it fails, this is a critical failure
                            throw new InvalidOperationException("CloudWatch Metrics client connection test failed", ex);
                        }
                    }
                }
                
                // Test logs client if not metrics-only
                if (!testMetricsOnly)
                {
                    try
                    {
                        await _logsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest { Limit = 1 });
                        testResults.Add("Logs client: OK");
                        _logger.LogInformation("CloudWatch Logs client connection test successful");
                    }
                    catch (Exception ex)
                    {
                        testResults.Add($"Logs client: Failed - {ex.Message}");
                        _logger.LogWarning(ex, "CloudWatch Logs client connection test failed");
                        
                        if (testLogsOnly)
                        {
                            // If we're only testing logs and it fails, this is a critical failure
                            throw new InvalidOperationException("CloudWatch Logs client connection test failed", ex);
                        }
                    }
                }
                
                _logger.LogInformation("CloudWatch connection test results: {Results}", string.Join(", ", testResults));
            }
            
            _logger.LogInformation("CloudWatch clients initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CloudWatch clients");
            return false;
        }
    }
    
    #region Metrics Operations
    
    /// <summary>
    /// List metrics with pagination support
    /// </summary>
    public async Task<List<Metric>> ListMetricsAsync(string? namespaceName = null, string? metricName = null, int maxRecords = 500)
    {
        await EnsureMetricsInitializedAsync();
        
        var request = new ListMetricsRequest();
        
        if (!string.IsNullOrEmpty(namespaceName))
        {
            request.Namespace = namespaceName;
        }
        
        if (!string.IsNullOrEmpty(metricName))
        {
            request.MetricName = metricName;
        }
        
        var allMetrics = new List<Metric>();
        string? nextToken = null;
        
        do
        {
            if (!string.IsNullOrEmpty(nextToken))
            {
                request.NextToken = nextToken;
            }
            
            var response = await _cloudWatchClient!.ListMetricsAsync(request);
            allMetrics.AddRange(response.Metrics);
            nextToken = response.NextToken;
            
            // Stop if we've reached the desired maximum
            if (allMetrics.Count >= maxRecords)
            {
                break;
            }
            
        } while (!string.IsNullOrEmpty(nextToken));
        
        return allMetrics.Take(maxRecords).ToList();
    }
    
    /// <summary>
    /// Get metric statistics
    /// </summary>
    public async Task<List<Datapoint>> GetMetricStatisticsAsync(
        string namespaceName,
        string metricName,
        DateTime startTime,
        DateTime endTime,
        int period,
        List<string> statistics,
        List<Dimension>? dimensions = null)
    {
        await EnsureMetricsInitializedAsync();
        
        var request = new GetMetricStatisticsRequest
        {
            Namespace = namespaceName,
            MetricName = metricName,
            StartTime = startTime,
            EndTime = endTime,
            Period = period,
            Statistics = statistics // This expects List<string> in newer AWS SDK versions
        };
        
        if (dimensions != null && dimensions.Count != 0)
        {
            request.Dimensions = dimensions;
        }
        
        var response = await _cloudWatchClient!.GetMetricStatisticsAsync(request);
        return response.Datapoints.OrderBy(d => d.Timestamp).ToList();
    }
    
    /// <summary>
    /// Put metric data
    /// </summary>
    public async Task<PutMetricDataResponse> PutMetricDataAsync(string namespaceName, List<MetricDatum> metricData)
    {
        await EnsureMetricsInitializedAsync();
        
        var request = new PutMetricDataRequest
        {
            Namespace = namespaceName,
            MetricData = metricData
        };
        
        return await _cloudWatchClient!.PutMetricDataAsync(request);
    }
    
    /// <summary>
    /// Create alarm
    /// </summary>
    public async Task<PutMetricAlarmResponse> CreateAlarmAsync(
        string alarmName,
        string alarmDescription,
        string metricName,
        string namespaceName,
        string statistic,
        int period,
        double threshold,
        string comparisonOperator,
        int evaluationPeriods,
        List<Dimension>? dimensions = null)
    {
        await EnsureMetricsInitializedAsync();
        
        var request = new PutMetricAlarmRequest
        {
            AlarmName = alarmName,
            AlarmDescription = alarmDescription,
            MetricName = metricName,
            Namespace = namespaceName,
            Statistic = statistic,
            Period = period,
            Threshold = threshold,
            ComparisonOperator = comparisonOperator,
            EvaluationPeriods = evaluationPeriods
        };
        
        if (dimensions != null && dimensions.Count != 0)
        {
            request.Dimensions = dimensions;
        }
        
        return await _cloudWatchClient!.PutMetricAlarmAsync(request);
    }
    
    /// <summary>
    /// List alarms
    /// </summary>
    public async Task<List<MetricAlarm>> ListAlarmsAsync(string? stateValue = null, int maxRecords = 100)
    {
        await EnsureMetricsInitializedAsync();
        
        var request = new DescribeAlarmsRequest
        {
            MaxRecords = maxRecords
        };
        
        if (!string.IsNullOrEmpty(stateValue))
        {
            request.StateValue = stateValue;
        }
        
        var response = await _cloudWatchClient!.DescribeAlarmsAsync(request);
        return response.MetricAlarms;
    }
    
    #endregion
    
    #region Logs Operations
    
    /// <summary>
    /// List log groups
    /// </summary>
    public async Task<List<LogGroup>> ListLogGroupsAsync(string? logGroupNamePrefix = null, int limit = 50)
    {
        await EnsureLogsInitializedAsync();
        
        var request = new DescribeLogGroupsRequest
        {
            Limit = limit
        };
        
        if (!string.IsNullOrEmpty(logGroupNamePrefix))
        {
            request.LogGroupNamePrefix = logGroupNamePrefix;
        }
        
        var response = await _logsClient!.DescribeLogGroupsAsync(request);
        return response.LogGroups;
    }
    
    /// <summary>
    /// List log streams
    /// </summary>
    public async Task<List<LogStream>> ListLogStreamsAsync(string logGroupName, int limit = 50)
    {
        await EnsureLogsInitializedAsync();
        
        var request = new DescribeLogStreamsRequest
        {
            LogGroupName = logGroupName,
            Limit = limit,
            OrderBy = OrderBy.LastEventTime,
            Descending = true
        };
        
        var response = await _logsClient!.DescribeLogStreamsAsync(request);
        return response.LogStreams;
    }
    
    /// <summary>
    /// Get log events
    /// </summary>
    public async Task<List<OutputLogEvent>> GetLogEventsAsync(
        string logGroupName,
        string logStreamName,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100)
    {
        await EnsureLogsInitializedAsync();
        
        var request = new GetLogEventsRequest
        {
            LogGroupName = logGroupName,
            LogStreamName = logStreamName,
            Limit = limit,
            StartFromHead = true
        };
        
        if (startTime.HasValue)
        {
            request.StartTime = startTime.Value;
        }
        
        if (endTime.HasValue)
        {
            request.EndTime = endTime.Value;
        }
        
        var response = await _logsClient!.GetLogEventsAsync(request);
        return response.Events;
    }
    
    /// <summary>
    /// Filter log events with pagination support.
    /// Returns one page of results at a time to avoid timeouts with large result sets.
    /// </summary>
    /// <param name="logGroupName">Name of the log group to search</param>
    /// <param name="filterPattern">Optional CloudWatch Logs filter pattern (e.g., "[ERROR]" or "[level=ERROR]")</param>
    /// <param name="startTime">Optional start time for the search range</param>
    /// <param name="endTime">Optional end time for the search range</param>
    /// <param name="limit">Maximum number of events to return per page (1-10000, default: 100)</param>
    /// <param name="nextToken">Continuation token from previous response (for pagination)</param>
    /// <returns>Paginated result containing events and pagination metadata</returns>
    public async Task<FilterLogEventsResult> FilterLogEventsAsync(
        string logGroupName,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        string? nextToken = null)
    {
        await EnsureLogsInitializedAsync();
        
        // Clamp limit to AWS allowed range (1-10,000)
        limit = Math.Clamp(limit, 1, 10000);
        
        var request = new FilterLogEventsRequest
        {
            LogGroupName = logGroupName,
            Limit = limit
        };
        
        // Add filter pattern if provided
        if (!string.IsNullOrEmpty(filterPattern))
        {
            request.FilterPattern = filterPattern;
        }
        
        // Convert start time to Unix milliseconds
        if (startTime.HasValue)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            request.StartTime = (long)(startTime.Value.ToUniversalTime() - epoch).TotalMilliseconds;
        }
        
        // Convert end time to Unix milliseconds
        if (endTime.HasValue)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            request.EndTime = (long)(endTime.Value.ToUniversalTime() - epoch).TotalMilliseconds;
        }
        
        // Add the continuation token for pagination
        if (!string.IsNullOrEmpty(nextToken))
        {
            request.NextToken = nextToken;
        }
        
        // Execute the API call (fast - single page only)
        var response = await _logsClient!.FilterLogEventsAsync(request);
        
        // Build paginated result
        var result = new FilterLogEventsResult
        {
            Events = response.Events ?? [],
            EventCount = response.Events?.Count ?? 0,
            HasMoreResults = !string.IsNullOrEmpty(response.NextToken),
            NextToken = response.NextToken,
            SearchedLogStreams = response.SearchedLogStreams,
            StartTime = startTime,
            EndTime = endTime
        };
        
        // Create a helpful summary message
        result.Summary =
            result.HasMoreResults
                ? $"Retrieved {result.EventCount} events. More results available - use NextToken to continue pagination."
                : $"Retrieved {result.EventCount} events. No more results available.";
        
        result.Summary += $" Searched {result.SearchedLogStreams.Count} log stream(s).";
        
        return result;
    }
    
    /// <summary>
    /// Create log group
    /// </summary>
    public async Task<CreateLogGroupResponse> CreateLogGroupAsync(string logGroupName)
    {
        await EnsureLogsInitializedAsync();
        
        var request = new CreateLogGroupRequest
        {
            LogGroupName = logGroupName
        };
        
        return await _logsClient!.CreateLogGroupAsync(request);
    }
    
    /// <summary>
    /// Delete log group
    /// </summary>
    public async Task<DeleteLogGroupResponse> DeleteLogGroupAsync(string logGroupName)
    {
        await EnsureLogsInitializedAsync();
        
        var request = new DeleteLogGroupRequest
        {
            LogGroupName = logGroupName
        };
        
        return await _logsClient!.DeleteLogGroupAsync(request);
    }
    
    /// <summary>
    /// Search CloudWatch logs using regex patterns with context
    /// </summary>
    public async Task<(List<LogSearchMatch> matches, LogSearchSummary summary)> SearchLogEventsWithRegexAsync(
        string logGroupName,
        string regexPattern,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int contextLines = 3,
        bool caseSensitive = false,
        int maxMatches = 100,
        int maxStreamsToSearch = 20)
    {
        var stopwatch = Stopwatch.StartNew();
        await EnsureLogsInitializedAsync();
        
        var regex = new Regex(regexPattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        var allMatches = new List<LogSearchMatch>();
        var logStreamsSearched = 0;
        
        try
        {
            // Get log streams, limited to prevent overwhelming searches
            var logStreams = await ListLogStreamsAsync(logGroupName, maxStreamsToSearch);
            
            foreach (var logStream in logStreams.Take(maxStreamsToSearch))
            {
                if (allMatches.Count >= maxMatches) break;
                
                logStreamsSearched++;
                
                try
                {
                    // Get log events for this stream
                    var logEvents = await GetLogEventsAsync(logGroupName, logStream.LogStreamName, startTime, endTime, 1000);
                    
                    // Convert to searchable format with line numbers
                    var searchableEvents = logEvents.Select((evt, idx) => new
                    {
                        Event = evt,
                        LineNumber = idx + 1,
                        Message = evt.Message ?? string.Empty
                    }).ToList();
                    
                    // Search through events
                    for (var i = 0; i < searchableEvents.Count; i++)
                    {
                        var searchEvent = searchableEvents[i];
                        var match = regex.Match(searchEvent.Message);
                        
                        if (match.Success)
                        {
                            var contextStart = Math.Max(0, i - contextLines);
                            var contextEnd = Math.Min(searchableEvents.Count - 1, i + contextLines);
                            
                            var logMatch = new LogSearchMatch
                            {
                                LogGroupName = logGroupName,
                                LogStreamName = logStream.LogStreamName,
                                Timestamp = searchEvent.Event.Timestamp,
                                IngestionTime = searchEvent.Event.IngestionTime,
                                EventId = $"{searchEvent.Event.Timestamp?.Ticks}-{searchEvent.LineNumber}",
                                LineNumber = searchEvent.LineNumber,
                                MatchedLine = searchEvent.Message.Trim(),
                                Context = searchableEvents
                                    .Skip(contextStart)
                                    .Take(contextEnd - contextStart + 1)
                                    .Select(e => new LogContextLine
                                    {
                                        LineNumber = e.LineNumber,
                                        Timestamp = e.Event.Timestamp,
                                        Content = e.Message.Trim(),
                                        IsMatch = e.LineNumber == searchEvent.LineNumber,
                                        LogStreamName = logStream.LogStreamName
                                    }).ToList(),
                                ExtractedValues = ExtractRegexGroups(match)
                            };
                            
                            allMatches.Add(logMatch);
                            
                            if (allMatches.Count >= maxMatches) break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to search log stream {LogStream} in group {LogGroup}", 
                        logStream.LogStreamName, logGroupName);
                }
            }
            
            stopwatch.Stop();
            
            var summary = GenerateLogSearchSummary(allMatches, logStreamsSearched, stopwatch.Elapsed);
            
            return (allMatches, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CloudWatch logs with regex pattern {Pattern}", regexPattern);
            throw;
        }
    }

    /// <summary>
    /// Search multiple log groups using regex patterns
    /// </summary>
    public async Task<(List<LogSearchMatch> matches, LogSearchSummary summary)> SearchMultipleLogGroupsWithRegexAsync(
        List<string> logGroupNames,
        string regexPattern,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int contextLines = 3,
        bool caseSensitive = false,
        int maxMatches = 100,
        int maxStreamsPerGroup = 5)
    {
        var stopwatch = Stopwatch.StartNew();
        var allMatches = new List<LogSearchMatch>();
        var totalStreamsSearched = 0;
        
        foreach (var logGroupName in logGroupNames)
        {
            if (allMatches.Count >= maxMatches) break;
            
            try
            {
                (var groupMatches, _) = await SearchLogEventsWithRegexAsync(
                    logGroupName, regexPattern, startTime, endTime, 
                    contextLines, caseSensitive, maxMatches - allMatches.Count, maxStreamsPerGroup);
                
                allMatches.AddRange(groupMatches);
                totalStreamsSearched += Math.Min(maxStreamsPerGroup, groupMatches.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search log group {LogGroup}", logGroupName);
            }
        }
        
        stopwatch.Stop();
        var summary = GenerateLogSearchSummary(allMatches, totalStreamsSearched, stopwatch.Elapsed);
        summary.LogGroupsSearched = logGroupNames.Count;
        
        return (allMatches, summary);
    }

    // Helper methods - add to CloudWatchService.cs
    private static List<string> ExtractRegexGroups(Match match)
    {
        var extractedValues = new List<string>();
        
        // Extract named and numbered groups
        for (var i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
            {
                extractedValues.Add(match.Groups[i].Value);
            }
        }
        
        return extractedValues;
    }

    private static LogSearchSummary GenerateLogSearchSummary(List<LogSearchMatch> matches, int streamsSearched, TimeSpan duration)
    {
        var summary = new LogSearchSummary
        {
            TotalMatches = matches.Count,
            LogStreamsSearched = streamsSearched,
            SearchDuration = duration
        };
        
        if (matches.Count != 0)
        {
            summary.FirstMatchTimestamp = matches.Min(m => m.Timestamp);
            summary.LastMatchTimestamp = matches.Max(m => m.Timestamp);
            
            // Analyze error patterns
            foreach (var match in matches)
            {
                var message = match.MatchedLine.ToLower();
                
                if (message.Contains("error")) summary.ErrorPatterns["Error"] = summary.ErrorPatterns.GetValueOrDefault("Error") + 1;
                if (message.Contains("warn")) summary.ErrorPatterns["Warning"] = summary.ErrorPatterns.GetValueOrDefault("Warning") + 1;
                if (message.Contains("exception")) summary.ErrorPatterns["Exception"] = summary.ErrorPatterns.GetValueOrDefault("Exception") + 1;
                if (message.Contains("timeout")) summary.ErrorPatterns["Timeout"] = summary.ErrorPatterns.GetValueOrDefault("Timeout") + 1;
                if (message.Contains("failed")) summary.ErrorPatterns["Failed"] = summary.ErrorPatterns.GetValueOrDefault("Failed") + 1;
                
                // Track log stream distribution
                summary.LogStreamDistribution[match.LogStreamName] = 
                    summary.LogStreamDistribution.GetValueOrDefault(match.LogStreamName) + 1;
            }
        }
        
        return summary;
    }
    
    #endregion
    
    /// <summary>
    /// Ensure metrics client is initialized and available (async version for auto-init support)
    /// </summary>
    private async Task EnsureMetricsInitializedAsync()
    {
        // Wait for auto-initialization to complete if still running
        if (!_cloudWatchInitialized)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!_cloudWatchInitialized && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
        }
        
        // Check if metrics client is available after auto-initialization
        if (_cloudWatchClient == null)
        {
            throw new InvalidOperationException(
                "CloudWatch Metrics client is not available. This may be due to insufficient permissions " +
                "(cloudwatch:ListMetrics required) or initialization failure. " +
                "Try explicit initialization with Initialize() or check your AWS permissions.");
        }
    }

    /// <summary>
    /// Ensure logs client is initialized and available (async version for auto-init support)  
    /// </summary>
    private async Task EnsureLogsInitializedAsync()
    {
        // Wait for auto-initialization to complete if still running
        if (!_cloudWatchInitialized)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!_cloudWatchInitialized && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
        }
        
        // Check if logs client is available after auto-initialization
        if (_logsClient == null)
        {
            throw new InvalidOperationException(
                "CloudWatch Logs client is not available. This may be due to insufficient permissions " +
                "(logs:DescribeLogGroups required) or initialization failure. " +
                "Try explicit initialization with Initialize() or check your AWS permissions.");
        }
    }

    /// <summary>
    /// Auto-initialize with discovered credentials - handle partial permissions gracefully
    /// </summary>
    private async Task AutoInitializeAsync()
    {
        try
        {
            if (_discoveryService.AutoInitialize())
            {
                var accountInfo = await _discoveryService.GetAccountInfoAsync();
                
                var config = new AwsConfiguration
                {
                    Region = accountInfo.InferredRegion,
                    ProfileName = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default"
                };
                
                // Try to initialize both clients, but allow partial success
                var metricsSuccess = await TryInitializeMetricsAsync(config);
                var logsSuccess = await TryInitializeLogsAsync(config);
                
                if (metricsSuccess || logsSuccess)
                {
                    _cloudWatchInitialized = true;
                    _logger.LogInformation("CloudWatch service auto-initialized: Metrics={MetricsAvailable}, Logs={LogsAvailable}", 
                        IsMetricsClientAvailable, IsLogsClientAvailable);
                }
                else
                {
                    _logger.LogWarning("CloudWatch service auto-initialization failed for both metrics and logs");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-initialize CloudWatch service. Explicit initialization may be required.");
        }
    }

    /// <summary>
    /// Try to initialize metrics client with permission testing
    /// </summary>
    private async Task<bool> TryInitializeMetricsAsync(AwsConfiguration config)
    {
        try
        {
            var clientConfig = new AmazonCloudWatchConfig
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps
            };
            
            var credentialsProvider = new AwsCredentialsProvider(config);
            var credentials = credentialsProvider.GetCredentials();
            
            _cloudWatchClient = credentials != null 
                ? new AmazonCloudWatchClient(credentials, clientConfig)
                : new AmazonCloudWatchClient(clientConfig);
            
            // Test permissions
            await _cloudWatchClient.ListMetricsAsync(new ListMetricsRequest());
            
            _logger.LogInformation("CloudWatch Metrics client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("CloudWatch Metrics client not available: {Message}", ex.Message);
            _cloudWatchClient?.Dispose();
            _cloudWatchClient = null;
            return false;
        }
    }

    /// <summary>
    /// Try to initialize logs client with permission testing
    /// </summary>
    private async Task<bool> TryInitializeLogsAsync(AwsConfiguration config)
    {
        try
        {
            var clientConfig = new AmazonCloudWatchLogsConfig
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps
            };
            
            var credentialsProvider = new AwsCredentialsProvider(config);
            var credentials = credentialsProvider.GetCredentials();
            
            _logsClient = credentials != null 
                ? new AmazonCloudWatchLogsClient(credentials, clientConfig)
                : new AmazonCloudWatchLogsClient(clientConfig);
            
            // Test permissions
            await _logsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest { Limit = 1 });
            
            _logger.LogInformation("CloudWatch Logs client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("CloudWatch Logs client not available: {Message}", ex.Message);
            _logsClient?.Dispose();
            _logsClient = null;
            return false;
        }
    }
    
    public void Dispose()
    {
        _cloudWatchClient?.Dispose();
        _logsClient?.Dispose();
    }
}
