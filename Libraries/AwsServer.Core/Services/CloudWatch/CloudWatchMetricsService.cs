using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;
using AwsServer.Core.Configuration;
using AwsServer.Core.Configuration.Models;
using Microsoft.Extensions.Logging;
using Metric = Amazon.CloudWatch.Model.Metric;

namespace AwsServer.Core.Services.CloudWatch;

/// <summary>
/// CloudWatch Metrics service with pagination-first design.
/// Exposes AWS native pagination tokens and modern GetMetricData API.
/// </summary>
public class CloudWatchMetricsService
{
    private readonly ILogger<CloudWatchMetricsService> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonCloudWatchClient? _cloudWatchClient;
    private AwsConfiguration? _config;

    public bool IsInitialized { get; private set; }

    public CloudWatchMetricsService(
        ILogger<CloudWatchMetricsService> logger,
        AwsDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        
        _ = Task.Run(AutoInitializeAsync);
    }

    /// <summary>
    /// Initialize CloudWatch Metrics client with configuration
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
            
            if (!string.IsNullOrEmpty(config.ServiceUrl))
            {
                clientConfig.ServiceURL = config.ServiceUrl;
            }
            
            var credentialsProvider = new AwsCredentialsProvider(config);
            AWSCredentials? credentials = credentialsProvider.GetCredentials();
            
            _cloudWatchClient = credentials != null
                ? new AmazonCloudWatchClient(credentials, clientConfig)
                : new AmazonCloudWatchClient(clientConfig);
            
            // Test connection
            await _cloudWatchClient.ListMetricsAsync();
            
            IsInitialized = true;
            _logger.LogInformation("CloudWatch Metrics client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CloudWatch Metrics client");
            IsInitialized = false;
            return false;
        }
    }

    #region Metrics Discovery

    /// <summary>
    /// List metrics with native AWS pagination support.
    /// Returns raw AWS response including NextToken for client-side pagination.
    /// </summary>
    public async Task<ListMetricsResponse> ListMetricsAsync(
        string? namespaceName = null,
        string? metricName = null,
        List<DimensionFilter>? dimensions = null,
        int maxRecords = 500,
        string? nextToken = null)
    {
        EnsureInitialized();
        
        var request = new ListMetricsRequest
        {
            NextToken = nextToken
        };
        
        if (!string.IsNullOrEmpty(namespaceName))
        {
            request.Namespace = namespaceName;
        }
        
        if (!string.IsNullOrEmpty(metricName))
        {
            request.MetricName = metricName;
        }
        
        if (dimensions != null && dimensions.Count > 0)
        {
            request.Dimensions = dimensions;
        }
        
        return await _cloudWatchClient!.ListMetricsAsync(request);
    }

    /// <summary>
    /// List all available namespaces.
    /// </summary>
    public async Task<List<string>> ListNamespacesAsync()
    {
        EnsureInitialized();
        
        var namespaces = new HashSet<string>();
        string? nextToken = null;
        
        do
        {
            ListMetricsResponse? response = await _cloudWatchClient!.ListMetricsAsync(new ListMetricsRequest
            {
                NextToken = nextToken
            });
            
            foreach (Metric? metric in response.Metrics)
            {
                if (!string.IsNullOrEmpty(metric.Namespace))
                {
                    namespaces.Add(metric.Namespace);
                }
            }
            
            nextToken = response.NextToken;
            
            // Limit to reasonable number to avoid long-running queries
            if (namespaces.Count > 1000)
            {
                break;
            }
            
        } while (!string.IsNullOrEmpty(nextToken));
        
        return namespaces.OrderBy(n => n).ToList();
    }

    #endregion

    #region Metric Statistics (Legacy API)

    /// <summary>
    /// Get metric statistics using the legacy API.
    /// For new applications, prefer GetMetricDataAsync which supports multiple metrics and math expressions.
    /// </summary>
    public async Task<GetMetricStatisticsResponse> GetMetricStatisticsAsync(
        string namespaceName,
        string metricName,
        DateTime startTime,
        DateTime endTime,
        int period,
        List<string> statistics,
        List<Dimension>? dimensions = null,
        string? unit = null)
    {
        EnsureInitialized();
        
        var request = new GetMetricStatisticsRequest
        {
            Namespace = namespaceName,
            MetricName = metricName,
            StartTime = startTime.ToUniversalTime(),
            EndTime = endTime.ToUniversalTime(),
            Period = period,
            Statistics = statistics
        };
        
        if (dimensions != null && dimensions.Count > 0)
        {
            request.Dimensions = dimensions;
        }
        
        if (!string.IsNullOrEmpty(unit))
        {
            request.Unit = unit;
        }
        
        return await _cloudWatchClient!.GetMetricStatisticsAsync(request);
    }

    #endregion

    #region Metric Data (Modern API)

    /// <summary>
    /// Get metric data using the modern GetMetricData API.
    /// Supports querying multiple metrics, math expressions, and more efficient data retrieval.
    /// </summary>
    public async Task<GetMetricDataResponse> GetMetricDataAsync(
        List<MetricDataQuery> metricDataQueries,
        DateTime startTime,
        DateTime endTime,
        string? nextToken = null,
        int maxDatapoints = 100800)
    {
        EnsureInitialized();
        
        var request = new GetMetricDataRequest
        {
            MetricDataQueries = metricDataQueries,
            StartTime = startTime.ToUniversalTime(),
            EndTime = endTime.ToUniversalTime(),
            MaxDatapoints = maxDatapoints,
            NextToken = nextToken
        };
        
        return await _cloudWatchClient!.GetMetricDataAsync(request);
    }

    #endregion

    #region Publishing Metrics

    /// <summary>
    /// Put metric data to CloudWatch.
    /// Can publish multiple metric data points in a single call (up to 1000).
    /// </summary>
    public async Task<PutMetricDataResponse> PutMetricDataAsync(
        string namespaceName,
        List<MetricDatum> metricData)
    {
        EnsureInitialized();
        
        if (metricData.Count > 1000)
        {
            throw new ArgumentException("Cannot publish more than 1000 metric data points in a single call", nameof(metricData));
        }
        
        var request = new PutMetricDataRequest
        {
            Namespace = namespaceName,
            MetricData = metricData
        };
        
        return await _cloudWatchClient!.PutMetricDataAsync(request);
    }

    #endregion

    #region Alarms - Full CRUD

    /// <summary>
    /// List metric alarms with pagination support.
    /// </summary>
    public async Task<DescribeAlarmsResponse> ListAlarmsAsync(
        string? alarmNamePrefix = null,
        List<string>? alarmNames = null,
        string? stateValue = null,
        string? actionPrefix = null,
        int maxRecords = 100,
        string? nextToken = null)
    {
        EnsureInitialized();
        
        var request = new DescribeAlarmsRequest
        {
            MaxRecords = Math.Clamp(maxRecords, 1, 100),
            NextToken = nextToken
        };
        
        if (!string.IsNullOrEmpty(alarmNamePrefix))
        {
            request.AlarmNamePrefix = alarmNamePrefix;
        }
        
        if (alarmNames != null && alarmNames.Count > 0)
        {
            request.AlarmNames = alarmNames;
        }
        
        if (!string.IsNullOrEmpty(stateValue))
        {
            request.StateValue = stateValue;
        }
        
        if (!string.IsNullOrEmpty(actionPrefix))
        {
            request.ActionPrefix = actionPrefix;
        }
        
        return await _cloudWatchClient!.DescribeAlarmsAsync(request);
    }

    /// <summary>
    /// Create or update a metric alarm.
    /// </summary>
    public async Task<PutMetricAlarmResponse> PutMetricAlarmAsync(
        string alarmName,
        string alarmDescription,
        string metricName,
        string namespaceName,
        string statistic,
        int period,
        int evaluationPeriods,
        double threshold,
        string comparisonOperator,
        List<Dimension>? dimensions = null,
        List<string>? alarmActions = null,
        List<string>? okActions = null,
        List<string>? insufficientDataActions = null,
        string? unit = null,
        bool actionsEnabled = true,
        int? datapointsToAlarm = null,
        string? treatMissingData = null)
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
            EvaluationPeriods = evaluationPeriods,
            Threshold = threshold,
            ComparisonOperator = comparisonOperator,
            ActionsEnabled = actionsEnabled
        };
        
        if (dimensions != null && dimensions.Count > 0)
        {
            request.Dimensions = dimensions;
        }
        
        if (alarmActions != null && alarmActions.Count > 0)
        {
            request.AlarmActions = alarmActions;
        }
        
        if (okActions != null && okActions.Count > 0)
        {
            request.OKActions = okActions;
        }
        
        if (insufficientDataActions != null && insufficientDataActions.Count > 0)
        {
            request.InsufficientDataActions = insufficientDataActions;
        }
        
        if (!string.IsNullOrEmpty(unit))
        {
            request.Unit = unit;
        }
        
        if (datapointsToAlarm.HasValue)
        {
            request.DatapointsToAlarm = datapointsToAlarm.Value;
        }
        
        if (!string.IsNullOrEmpty(treatMissingData))
        {
            request.TreatMissingData = treatMissingData;
        }
        
        return await _cloudWatchClient!.PutMetricAlarmAsync(request);
    }

    /// <summary>
    /// Delete metric alarms.
    /// </summary>
    public async Task<DeleteAlarmsResponse> DeleteAlarmsAsync(List<string> alarmNames)
    {
        EnsureInitialized();
        
        if (alarmNames.Count == 0)
        {
            throw new ArgumentException("Must specify at least one alarm name", nameof(alarmNames));
        }
        
        if (alarmNames.Count > 100)
        {
            throw new ArgumentException("Cannot delete more than 100 alarms in a single call", nameof(alarmNames));
        }
        
        var request = new DeleteAlarmsRequest
        {
            AlarmNames = alarmNames
        };
        
        return await _cloudWatchClient!.DeleteAlarmsAsync(request);
    }

    /// <summary>
    /// Set alarm state (for testing/debugging).
    /// </summary>
    public async Task<SetAlarmStateResponse> SetAlarmStateAsync(
        string alarmName,
        string stateValue,
        string stateReason,
        string? stateReasonData = null)
    {
        EnsureInitialized();
        
        var request = new SetAlarmStateRequest
        {
            AlarmName = alarmName,
            StateValue = stateValue,
            StateReason = stateReason
        };
        
        if (!string.IsNullOrEmpty(stateReasonData))
        {
            request.StateReasonData = stateReasonData;
        }
        
        return await _cloudWatchClient!.SetAlarmStateAsync(request);
    }

    /// <summary>
    /// Enable alarm actions.
    /// </summary>
    public async Task<EnableAlarmActionsResponse> EnableAlarmActionsAsync(List<string> alarmNames)
    {
        EnsureInitialized();
        
        var request = new EnableAlarmActionsRequest
        {
            AlarmNames = alarmNames
        };
        
        return await _cloudWatchClient!.EnableAlarmActionsAsync(request);
    }

    /// <summary>
    /// Disable alarm actions.
    /// </summary>
    public async Task<DisableAlarmActionsResponse> DisableAlarmActionsAsync(List<string> alarmNames)
    {
        EnsureInitialized();
        
        var request = new DisableAlarmActionsRequest
        {
            AlarmNames = alarmNames
        };
        
        return await _cloudWatchClient!.DisableAlarmActionsAsync(request);
    }

    /// <summary>
    /// Describe alarm history.
    /// </summary>
    public async Task<DescribeAlarmHistoryResponse> DescribeAlarmHistoryAsync(
        string? alarmName = null,
        List<string>? alarmTypes = null,
        string? historyItemType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int maxRecords = 100,
        string? nextToken = null)
    {
        EnsureInitialized();
        
        var request = new DescribeAlarmHistoryRequest
        {
            MaxRecords = Math.Clamp(maxRecords, 1, 100),
            NextToken = nextToken
        };
        
        if (!string.IsNullOrEmpty(alarmName))
        {
            request.AlarmName = alarmName;
        }
        
        if (alarmTypes != null && alarmTypes.Count > 0)
        {
            request.AlarmTypes = alarmTypes;
        }
        
        if (!string.IsNullOrEmpty(historyItemType))
        {
            request.HistoryItemType = historyItemType;
        }
        
        if (startDate.HasValue)
        {
            request.StartDate = startDate.Value.ToUniversalTime();
        }
        
        if (endDate.HasValue)
        {
            request.EndDate = endDate.Value.ToUniversalTime();
        }
        
        return await _cloudWatchClient!.DescribeAlarmHistoryAsync(request);
    }

    #endregion

    #region Helper Methods

    private void EnsureInitialized()
    {
        if (!IsInitialized || _cloudWatchClient == null)
        {
            throw new InvalidOperationException(
                "CloudWatch Metrics client is not initialized. " +
                "Call InitializeAsync() or wait for auto-initialization to complete.");
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
                    AccessKeyId = null,
                    SecretAccessKey = null
                };
                
                await InitializeAsync(config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Auto-initialization of CloudWatch Metrics client failed");
        }
    }

    #endregion
}
