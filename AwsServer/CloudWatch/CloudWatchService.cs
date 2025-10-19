using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using AwsServer.Configuration;
using AwsServer.Configuration.Models;
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
    /// Check if the metrics client is available
    /// </summary>
    public bool IsMetricsClientAvailable => _cloudWatchClient != null;

    /// <summary>
    /// Check if the logs client is available
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
    /// <param name="testConnection">Whether to test the connection during initialization (default: true)</param>
    /// <param name="testMetricsOnly">If testing, only test metrics client (default: false)</param>
    /// <param name="testLogsOnly">If testing, only test logs the client (default: false)</param>
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
                            // If we're only testing metrics, and it fails, this is a critical failure
                            throw new InvalidOperationException("CloudWatch Metrics client connection test failed", ex);
                        }
                    }
                }
                
                // Test logs the client if not metrics-only
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
        
        GetMetricStatisticsResponse? response = await _cloudWatchClient!.GetMetricStatisticsAsync(request);
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
        await EnsureLogsInitializedAsync();
        
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
        await EnsureLogsInitializedAsync();
        
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
        
        GetLogEventsResponse? response = await _logsClient!.GetLogEventsAsync(request);
        return response.Events;
    }
    
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
    
    
    #endregion
    
    /// <summary>
    /// Ensure the metrics client is initialized and available (async version for auto-init support)
    /// </summary>
    private async Task EnsureMetricsInitializedAsync()
    {
        // Wait for auto-initialization to complete if still running
        if (!_cloudWatchInitialized)
        {
            DateTime timeout = DateTime.UtcNow.AddSeconds(5);
            while (!_cloudWatchInitialized && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
        }
        
        // Check if the metrics client is available after auto-initialization
        if (_cloudWatchClient == null)
        {
            throw new InvalidOperationException(
                "CloudWatch Metrics client is not available. This may be due to insufficient permissions " +
                "(cloudwatch:ListMetrics required) or initialization failure. " +
                "Try explicit initialization with Initialize() or check your AWS permissions.");
        }
    }

    /// <summary>
    /// Ensure the logs client is initialized and available (async version for auto-init support)  
    /// </summary>
    private async Task EnsureLogsInitializedAsync()
    {
        // Wait for auto-initialization to complete if still running
        if (!_cloudWatchInitialized)
        {
            DateTime timeout = DateTime.UtcNow.AddSeconds(5);
            while (!_cloudWatchInitialized && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
        }
        
        // Check if the logs client is available after auto-initialization
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
                AccountInfo accountInfo = await _discoveryService.GetAccountInfoAsync();
                
                var config = new AwsConfiguration
                {
                    Region = accountInfo.InferredRegion,
                    ProfileName = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default"
                };
                
                // Try to initialize both clients but allow partial success
                bool metricsSuccess = await TryInitializeMetricsAsync(config);
                bool logsSuccess = await TryInitializeLogsAsync(config);
                
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
    /// Try to initialize the metrics client with permission testing
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
            AWSCredentials? credentials = credentialsProvider.GetCredentials();
            
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
    /// Try to initialize the logs client with permission testing
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
            AWSCredentials? credentials = credentialsProvider.GetCredentials();
            
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