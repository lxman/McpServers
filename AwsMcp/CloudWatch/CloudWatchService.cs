using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using AwsMcp.Configuration;
using Microsoft.Extensions.Logging;
using Metric = Amazon.CloudWatch.Model.Metric;

namespace AwsMcp.CloudWatch;

/// <summary>
/// Service for CloudWatch operations (metrics and logs)
/// </summary>
public class CloudWatchService
{
    private readonly ILogger<CloudWatchService> _logger;
    private AmazonCloudWatchClient? _cloudWatchClient;
    private AmazonCloudWatchLogsClient? _logsClient;
    private AwsConfiguration? _config;
    
    public CloudWatchService(ILogger<CloudWatchService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Initialize CloudWatch clients with configuration
    /// </summary>
    public async Task<bool> InitializeAsync(AwsConfiguration config)
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
            AWSCredentials? credentials = credentialsProvider.GetCredentials();
            
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
            
            // Test connection - use a simple request that works with pagination
            await _cloudWatchClient.ListMetricsAsync(new ListMetricsRequest());
            await _logsClient.DescribeLogGroupsAsync(new DescribeLogGroupsRequest { Limit = 1 });
            
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
        EnsureInitialized();
        
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
            
            ListMetricsResponse? response = await _cloudWatchClient!.ListMetricsAsync(request);
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
        EnsureInitialized();
        
        var request = new GetMetricStatisticsRequest
        {
            Namespace = namespaceName,
            MetricName = metricName,
            StartTime = startTime,
            EndTime = endTime,
            Period = period,
            Statistics = statistics // This expects List<string> in newer AWS SDK versions
        };
        
        if (dimensions != null && dimensions.Any())
        {
            request.Dimensions = dimensions;
        }
        
        GetMetricStatisticsResponse? response = await _cloudWatchClient!.GetMetricStatisticsAsync(request);
        return response.Datapoints.OrderBy(d => d.Timestamp).ToList();
    }
    
    /// <summary>
    /// Put metric data
    /// </summary>
    public async Task<PutMetricDataResponse> PutMetricDataAsync(string namespaceName, List<MetricDatum> metricData)
    {
        EnsureInitialized();
        
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
        EnsureInitialized();
        
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
        
        if (dimensions != null && dimensions.Any())
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
        EnsureInitialized();
        
        var request = new DescribeAlarmsRequest
        {
            MaxRecords = maxRecords
        };
        
        if (!string.IsNullOrEmpty(stateValue))
        {
            request.StateValue = stateValue;
        }
        
        DescribeAlarmsResponse? response = await _cloudWatchClient!.DescribeAlarmsAsync(request);
        return response.MetricAlarms;
    }
    
    #endregion
    
    #region Logs Operations
    
    /// <summary>
    /// List log groups
    /// </summary>
    public async Task<List<LogGroup>> ListLogGroupsAsync(string? logGroupNamePrefix = null, int limit = 50)
    {
        EnsureInitialized();
        
        var request = new DescribeLogGroupsRequest
        {
            Limit = limit
        };
        
        if (!string.IsNullOrEmpty(logGroupNamePrefix))
        {
            request.LogGroupNamePrefix = logGroupNamePrefix;
        }
        
        DescribeLogGroupsResponse? response = await _logsClient!.DescribeLogGroupsAsync(request);
        return response.LogGroups;
    }
    
    /// <summary>
    /// List log streams
    /// </summary>
    public async Task<List<LogStream>> ListLogStreamsAsync(string logGroupName, int limit = 50)
    {
        EnsureInitialized();
        
        var request = new DescribeLogStreamsRequest
        {
            LogGroupName = logGroupName,
            Limit = limit,
            OrderBy = OrderBy.LastEventTime,
            Descending = true
        };
        
        DescribeLogStreamsResponse? response = await _logsClient!.DescribeLogStreamsAsync(request);
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
        EnsureInitialized();
        
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
        
        GetLogEventsResponse? response = await _logsClient!.GetLogEventsAsync(request);
        return response.Events;
    }
    
    /// <summary>
    /// Filter log events
    /// </summary>
    public async Task<List<FilteredLogEvent>> FilterLogEventsAsync(
        string logGroupName,
        string? filterPattern = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100)
    {
        EnsureInitialized();
        
        var request = new FilterLogEventsRequest
        {
            LogGroupName = logGroupName,
            Limit = limit
        };
        
        if (!string.IsNullOrEmpty(filterPattern))
        {
            request.FilterPattern = filterPattern;
        }
        
        if (startTime.HasValue)
        {
            // Convert to Unix timestamp in milliseconds
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            request.StartTime = (long)(startTime.Value.ToUniversalTime() - epoch).TotalMilliseconds;
        }
        
        if (endTime.HasValue)
        {
            // Convert to Unix timestamp in milliseconds
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            request.EndTime = (long)(endTime.Value.ToUniversalTime() - epoch).TotalMilliseconds;
        }
        
        FilterLogEventsResponse? response = await _logsClient!.FilterLogEventsAsync(request);
        return response.Events;
    }
    
    /// <summary>
    /// Create log group
    /// </summary>
    public async Task<CreateLogGroupResponse> CreateLogGroupAsync(string logGroupName)
    {
        EnsureInitialized();
        
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
        EnsureInitialized();
        
        var request = new DeleteLogGroupRequest
        {
            LogGroupName = logGroupName
        };
        
        return await _logsClient!.DeleteLogGroupAsync(request);
    }
    
    #endregion
    
    private void EnsureInitialized()
    {
        if (_cloudWatchClient == null || _logsClient == null)
        {
            throw new System.InvalidOperationException("CloudWatch clients are not initialized. Call InitializeAsync first.");
        }
    }
    
    public void Dispose()
    {
        _cloudWatchClient?.Dispose();
        _logsClient?.Dispose();
    }
}
