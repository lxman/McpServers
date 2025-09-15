using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using AwsMcp.CloudWatch;
using AwsMcp.Configuration;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs.Model;
using InvalidOperationException = Amazon.CloudWatchLogs.Model.InvalidOperationException;

namespace AwsMcp.Tools;

[McpServerToolType]
public class CloudWatchTools
{
    private readonly CloudWatchService _cloudWatchService;

    public CloudWatchTools(CloudWatchService cloudWatchService)
    {
        _cloudWatchService = cloudWatchService;
    }

    [McpServerTool]
    [Description("Initialize CloudWatch service with AWS credentials and configuration")]
    public async Task<string> InitializeCloudWatch(
        [Description("AWS region (default: us-east-1)")]
        string region = "us-east-1",
        [Description("AWS Access Key ID (optional if using profile or environment)")]
        string? accessKeyId = null,
        [Description("AWS Secret Access Key (optional if using profile or environment)")]
        string? secretAccessKey = null,
        [Description("AWS Profile name (optional)")]
        string? profileName = null,
        [Description("Custom service URL for LocalStack or other endpoints (optional)")]
        string? serviceUrl = null)
    {
        try
        {
            var config = new AwsConfiguration
            {
                Region = region,
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                ProfileName = profileName,
                ServiceUrl = serviceUrl
            };

            bool success = await _cloudWatchService.InitializeAsync(config);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "CloudWatch service initialized successfully" : "Failed to initialize CloudWatch service",
                region,
                usingProfile = !string.IsNullOrEmpty(profileName),
                usingCustomEndpoint = !string.IsNullOrEmpty(serviceUrl)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "initializing CloudWatch service");
        }
    }

    #region Metrics Operations

    [McpServerTool]
    [Description("List CloudWatch metrics")]
    public async Task<string> ListMetrics(
        [Description("Namespace to filter metrics (optional, e.g., 'AWS/EC2', 'AWS/S3')")]
        string? namespaceName = null,
        [Description("Metric name to filter (optional)")]
        string? metricName = null,
        [Description("Maximum number of metrics to return (default: 500)")]
        int maxRecords = 500)
    {
        try
        {
            List<Metric> metrics = await _cloudWatchService.ListMetricsAsync(namespaceName, metricName, maxRecords);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                metricCount = metrics.Count,
                namespaceName,
                metricName,
                metrics = metrics.Select(m => new
                {
                    namespaceName = m.Namespace,
                    metricName = m.MetricName,
                    dimensions = m.Dimensions?.Select(d => new { name = d.Name, value = d.Value }).ToList()
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "listing CloudWatch metrics");
        }
    }

    [McpServerTool]
    [Description("Get CloudWatch metric statistics")]
    public async Task<string> GetMetricStatistics(
        [Description("Namespace of the metric (e.g., 'AWS/EC2', 'AWS/S3')")]
        string namespaceName,
        [Description("Name of the metric")]
        string metricName,
        [Description("Start time (ISO 8601 format, e.g., '2025-09-09T10:00:00Z')")]
        string startTime,
        [Description("End time (ISO 8601 format, e.g., '2025-09-09T11:00:00Z')")]
        string endTime,
        [Description("Period in seconds (e.g., 300 for 5 minutes)")]
        int period,
        [Description("Statistics to retrieve (comma-separated: Average,Sum,Maximum,Minimum,SampleCount)")]
        string statistics,
        [Description("Dimensions as JSON array (optional, e.g., '[{\"Name\":\"InstanceId\",\"Value\":\"i-1234567890abcdef0\"}]')")]
        string? dimensionsJson = null)
    {
        try
        {
            DateTime start = DateTime.Parse(startTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            DateTime end = DateTime.Parse(endTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            
            List<string> statisticsList = statistics.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList();
            
            List<Dimension>? dimensions = null;
            if (!string.IsNullOrEmpty(dimensionsJson))
            {
                var dimensionData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(dimensionsJson);
                dimensions = dimensionData?.Select(d => new Dimension
                {
                    Name = d["Name"],
                    Value = d["Value"]
                }).ToList();
            }
            
            List<Datapoint> datapoints = await _cloudWatchService.GetMetricStatisticsAsync(
                namespaceName, metricName, start, end, period, statisticsList, dimensions);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                namespaceName,
                metricName,
                startTime = start,
                endTime = end,
                period,
                statistics = statisticsList,
                datapointCount = datapoints.Count,
                datapoints = datapoints.Select(d => new
                {
                    timestamp = d.Timestamp,
                    average = d.Average,
                    sum = d.Sum,
                    maximum = d.Maximum,
                    minimum = d.Minimum,
                    sampleCount = d.SampleCount,
                    unit = d.Unit?.Value
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "getting CloudWatch metric statistics");
        }
    }

    [McpServerTool]
    [Description("Put custom metric data to CloudWatch")]
    public async Task<string> PutMetricData(
        [Description("Namespace for the custom metric")]
        string namespaceName,
        [Description("Name of the metric")]
        string metricName,
        [Description("Value of the metric")]
        double value,
        [Description("Unit of the metric (e.g., 'Count', 'Percent', 'Seconds')")]
        string unit = "Count",
        [Description("Dimensions as JSON array (optional, e.g., '[{\"Name\":\"Environment\",\"Value\":\"Production\"}]')")]
        string? dimensionsJson = null,
        [Description("Timestamp (ISO 8601 format, optional - defaults to now)")]
        string? timestamp = null)
    {
        try
        {
            var metricDatum = new MetricDatum
            {
                MetricName = metricName,
                Value = value,
                Unit = unit, // Use string directly
                Timestamp = timestamp != null 
                    ? DateTime.Parse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind)
                    : DateTime.UtcNow
            };
            
            if (!string.IsNullOrEmpty(dimensionsJson))
            {
                var dimensionData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(dimensionsJson);
                metricDatum.Dimensions = dimensionData?.Select(d => new Dimension
                {
                    Name = d["Name"],
                    Value = d["Value"]
                }).ToList();
            }
            
            await _cloudWatchService.PutMetricDataAsync(namespaceName, [metricDatum]);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Metric data published successfully",
                namespaceName,
                metricName,
                value,
                unit,
                timestamp = metricDatum.Timestamp
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "putting CloudWatch metric data");
        }
    }

    [McpServerTool]
    [Description("Create a CloudWatch alarm")]
    public async Task<string> CreateAlarm(
        [Description("Name of the alarm")]
        string alarmName,
        [Description("Description of the alarm")]
        string alarmDescription,
        [Description("Namespace of the metric")]
        string namespaceName,
        [Description("Name of the metric")]
        string metricName,
        [Description("Statistic (Average, Sum, Maximum, Minimum, SampleCount)")]
        string statistic,
        [Description("Period in seconds")]
        int period,
        [Description("Threshold value")]
        double threshold,
        [Description("Comparison operator (GreaterThanThreshold, LessThanThreshold, etc.)")]
        string comparisonOperator,
        [Description("Number of evaluation periods")]
        int evaluationPeriods,
        [Description("Dimensions as JSON array (optional)")]
        string? dimensionsJson = null)
    {
        try
        {
            List<Dimension>? dimensions = null;
            if (!string.IsNullOrEmpty(dimensionsJson))
            {
                var dimensionData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(dimensionsJson);
                dimensions = dimensionData?.Select(d => new Dimension
                {
                    Name = d["Name"],
                    Value = d["Value"]
                }).ToList();
            }
            
            await _cloudWatchService.CreateAlarmAsync(
                alarmName,
                alarmDescription,
                metricName,
                namespaceName,
                statistic,
                period,
                threshold,
                comparisonOperator,
                evaluationPeriods,
                dimensions);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Alarm created successfully",
                alarmName,
                metricName,
                threshold,
                comparisonOperator
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "creating CloudWatch alarm");
        }
    }

    [McpServerTool]
    [Description("List CloudWatch alarms")]
    public async Task<string> ListAlarms(
        [Description("Filter by alarm state (OK, ALARM, INSUFFICIENT_DATA) - optional")]
        string? stateValue = null,
        [Description("Maximum number of alarms to return (default: 100)")]
        int maxRecords = 100)
    {
        try
        {
            List<MetricAlarm> alarms = await _cloudWatchService.ListAlarmsAsync(stateValue, maxRecords);
        
            return JsonSerializer.Serialize(new
            {
                success = true,
                alarmCount = alarms.Count,
                stateFilter = stateValue,
                alarms = alarms.Select(a => new
                {
                    alarmName = a.AlarmName,
                    alarmDescription = a.AlarmDescription,
                    metricName = a.MetricName,
                    namespaceName = a.Namespace,
                    statistic = a.Statistic,
                    period = a.Period,
                    threshold = a.Threshold,
                    comparisonOperator = a.ComparisonOperator?.Value,
                    stateValue = a.StateValue?.Value,
                    stateReason = a.StateReason,
                    stateUpdatedTimestamp = a.StateUpdatedTimestamp
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "listing CloudWatch alarms");
        }
    }

    #endregion

    #region Logs Operations

    [McpServerTool]
    [Description("List CloudWatch log groups")]
    public async Task<string> ListLogGroups(
        [Description("Log group name prefix to filter (optional)")]
        string? logGroupNamePrefix = null,
        [Description("Maximum number of log groups to return (default: 50)")]
        int limit = 50)
    {
        try
        {
            List<LogGroup> logGroups = await _cloudWatchService.ListLogGroupsAsync(logGroupNamePrefix, limit);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                logGroupCount = logGroups.Count,
                logGroupNamePrefix,
                logGroups = logGroups.Select(lg => new
                {
                    logGroupName = lg.LogGroupName,
                    creationTime = lg.CreationTime,
                    retentionInDays = lg.RetentionInDays,
                    storedBytes = lg.StoredBytes,
                    metricFilterCount = lg.MetricFilterCount
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "listing CloudWatch log groups");
        }
    }

    [McpServerTool]
    [Description("List log streams in a CloudWatch log group")]
    public async Task<string> ListLogStreams(
        [Description("Name of the log group")]
        string logGroupName,
        [Description("Maximum number of log streams to return (default: 50)")]
        int limit = 50)
    {
        try
        {
            List<LogStream> logStreams = await _cloudWatchService.ListLogStreamsAsync(logGroupName, limit);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                logGroupName,
                logStreamCount = logStreams.Count,
                logStreams = logStreams.Select(ls => new
                {
                    logStreamName = ls.LogStreamName,
                    creationTime = ls.CreationTime,
                    lastIngestionTime = ls.LastIngestionTime
                    // Note: Removed FirstEventTime and LastEventTime as they may not exist in current AWS SDK
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "listing CloudWatch log streams");
        }
    }

    [McpServerTool]
    [Description("Get log events from a specific log stream")]
    public async Task<string> GetLogEvents(
        [Description("Name of the log group")]
        string logGroupName,
        [Description("Name of the log stream")]
        string logStreamName,
        [Description("Start time (ISO 8601 format, optional)")]
        string? startTime = null,
        [Description("End time (ISO 8601 format, optional)")]
        string? endTime = null,
        [Description("Maximum number of log events to return (default: 100)")]
        int limit = 100)
    {
        try
        {
            DateTime? start = null;
            DateTime? end = null;
            
            if (!string.IsNullOrEmpty(startTime))
            {
                start = DateTime.Parse(startTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }
            
            if (!string.IsNullOrEmpty(endTime))
            {
                end = DateTime.Parse(endTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }
            
            List<OutputLogEvent> logEvents = await _cloudWatchService.GetLogEventsAsync(logGroupName, logStreamName, start, end, limit);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                logGroupName,
                logStreamName,
                eventCount = logEvents.Count,
                startTime = start,
                endTime = end,
                events = logEvents.Select(e => new
                {
                    timestamp = e.Timestamp,
                    message = e.Message,
                    ingestionTime = e.IngestionTime
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "getting CloudWatch log events");
        }
    }

    [McpServerTool]
    [Description("Filter log events across log streams using a filter pattern")]
    public async Task<string> FilterLogEvents(
        [Description("Name of the log group")]
        string logGroupName,
        [Description("Filter pattern (optional, e.g., 'ERROR', '[timestamp, request_id, message]')")]
        string? filterPattern = null,
        [Description("Start time (ISO 8601 format, optional)")]
        string? startTime = null,
        [Description("End time (ISO 8601 format, optional)")]
        string? endTime = null,
        [Description("Maximum number of log events to return (default: 100)")]
        int limit = 100)
    {
        try
        {
            DateTime? start = null;
            DateTime? end = null;
            
            if (!string.IsNullOrEmpty(startTime))
            {
                start = DateTime.Parse(startTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }
            
            if (!string.IsNullOrEmpty(endTime))
            {
                end = DateTime.Parse(endTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }
            
            List<FilteredLogEvent> logEvents = await _cloudWatchService.FilterLogEventsAsync(logGroupName, filterPattern, start, end, limit);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                logGroupName,
                filterPattern,
                eventCount = logEvents.Count,
                startTime = start,
                endTime = end,
                events = logEvents.Select(e => new
                {
                    logStreamName = e.LogStreamName,
                    timestamp = e.Timestamp,
                    message = e.Message,
                    ingestionTime = e.IngestionTime,
                    eventId = e.EventId
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "filtering CloudWatch log events");
        }
    }

    [McpServerTool]
    [Description("Create a new CloudWatch log group")]
    public async Task<string> CreateLogGroup(
        [Description("Name of the log group to create")]
        string logGroupName)
    {
        try
        {
            await _cloudWatchService.CreateLogGroupAsync(logGroupName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Log group created successfully",
                logGroupName
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "creating CloudWatch log group");
        }
    }

    [McpServerTool]
    [Description("Delete a CloudWatch log group")]
    public async Task<string> DeleteLogGroup(
        [Description("Name of the log group to delete")]
        string logGroupName)
    {
        try
        {
            await _cloudWatchService.DeleteLogGroupAsync(logGroupName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Log group deleted successfully",
                logGroupName
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "deleting CloudWatch log group");
        }
    }

    #endregion
    
    /// <summary>
    /// Enhanced error handling for AWS operations with user-friendly messages
    /// </summary>
    private static string HandleError(Exception ex, string operation)
    {
        object error;

        switch (ex)
        {
            case InvalidOperationException invalidOpEx:
                error = new
                {
                    success = false,
                    error = "CloudWatch service not initialized or missing permissions",
                    details = invalidOpEx.Message,
                    suggestedActions = new[]
                    {
                        "Ensure you have called InitializeCloudWatch first",
                        "Check your AWS credentials and permissions", 
                        "Verify your AWS region is correct",
                        "For partial permissions, try initializing with testMetricsOnly or testLogsOnly parameters"
                    },
                    errorType = "ServiceNotInitialized"
                };
                break;
            case Amazon.Runtime.AmazonServiceException awsEx:
            {
                string[] actions = awsEx.ErrorCode switch
                {
                    "AccessDenied" => new[]
                    {
                        "Check your IAM permissions for CloudWatch",
                        "Ensure your user/role has CloudWatch:ListMetrics, CloudWatch:DescribeAlarms, or CloudWatch Logs permissions",
                        "Try: aws sts get-caller-identity to verify your credentials"
                    },
                    "UnauthorizedOperation" => new[]
                    {
                        "Your AWS credentials don't have permission for CloudWatch operations",
                        "Contact your AWS administrator to grant CloudWatch permissions",
                        "Required permissions depend on the operation (metrics vs logs)"
                    },
                    "InvalidParameterValue" => new[]
                    {
                        "Check the parameters passed to the operation",
                        "Verify metric names, namespaces, and time ranges are valid",
                        "Ensure alarm names don't contain invalid characters"
                    },
                    "Throttling" => new[]
                    {
                        "AWS is throttling your CloudWatch requests",
                        "Wait 30-60 seconds and try again",
                        "Consider reducing the frequency of your requests"
                    },
                    "ResourceNotFound" => new[]
                    {
                        "The specified CloudWatch resource doesn't exist",
                        "Check log group names, alarm names, and metric names",
                        "Verify you're in the correct AWS region"
                    },
                    _ => new[]
                    {
                        "Check AWS CloudWatch service status at https://status.aws.amazon.com/",
                        "Verify your parameters and try again",
                        $"AWS Error Code: {awsEx.ErrorCode} - consult AWS documentation"
                    }
                };

                error = new
                {
                    success = false,
                    error = $"AWS CloudWatch service error: {awsEx.ErrorCode}",
                    details = awsEx.Message,
                    suggestedActions = actions,
                    errorType = "AWSService",
                    statusCode = awsEx.StatusCode.ToString(),
                    awsErrorCode = awsEx.ErrorCode
                };
                break;
            }
            case Amazon.Runtime.AmazonClientException clientEx:
                error = new
                {
                    success = false,
                    error = "AWS client error - network or configuration issue",
                    details = clientEx.Message,
                    suggestedActions = new[]
                    {
                        "Check your internet connection",
                        "Verify AWS endpoint configuration",
                        "Check if you're behind a firewall or proxy",
                        "Try with a different AWS region",
                        "Verify your AWS service URL if using LocalStack"
                    },
                    errorType = "NetworkOrConfiguration"
                };
                break;
            case ArgumentException argEx:
                error = new
                {
                    success = false,
                    error = "Invalid parameter provided to CloudWatch operation",
                    details = argEx.Message,
                    suggestedActions = new[]
                    {
                        "Check the format of date/time parameters (use ISO 8601 format like '2025-09-12T20:00:00Z')",
                        "Verify all required parameters are provided",
                        "Ensure numeric parameters (period, limit) are within valid ranges",
                        "Check that alarm states are: OK, ALARM, or INSUFFICIENT_DATA"
                    },
                    errorType = "InvalidParameter"
                };
                break;
            case JsonException jsonEx:
                error = new
                {
                    success = false,
                    error = "Invalid JSON format in parameters",
                    details = jsonEx.Message,
                    suggestedActions = new[]
                    {
                        "Check the format of JSON parameters like dimensionsJson",
                        "Ensure JSON is properly escaped: [{\"Name\":\"Environment\",\"Value\":\"Production\"}]",
                        "Use online JSON validators to verify your JSON syntax",
                        "Common issue: Use double quotes, not single quotes in JSON"
                    },
                    errorType = "InvalidJSON"
                };
                break;
            default:
                error = new
                {
                    success = false,
                    error = $"Unexpected error {operation}",
                    details = ex.Message,
                    suggestedActions = new[]
                    {
                        "Check the operation parameters are correct",
                        "Verify your AWS configuration and credentials",
                        "Try the operation again after a brief wait",
                        "Contact support if the issue persists",
                        $"Exception type: {ex.GetType().Name}"
                    },
                    errorType = "Unexpected",
                    exceptionType = ex.GetType().Name
                };
                break;
        }

        return JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true });
    }
}
