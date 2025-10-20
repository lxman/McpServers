using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.Core;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.ApplicationInsights;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.OperationalInsights;
using Azure.ResourceManager.Resources;
using AzureServer.Services.Core;
using AzureServer.Services.Monitor.Models;
using MetricValue = AzureServer.Services.Monitor.Models.MetricValue;

namespace AzureServer.Services.Monitor;

public class MonitorService(
    ArmClientFactory armClientFactory,
    ILogger<MonitorService> logger)
    : IMonitorService
{
    private LogsQueryClient? _logsClient;
    private MetricsQueryClient? _metricsClient;

    private async Task<(LogsQueryClient logs, MetricsQueryClient metrics)> GetQueryClientsAsync()
    {
        // Check if we need to recreate the query clients
        ArmClientInfo? clientInfo = armClientFactory.GetCurrentClientInfo();
    
        if (_logsClient is not null && _metricsClient is not null && clientInfo is not null) 
            return (_logsClient, _metricsClient);
    
        // Use the factory to get the credential
        TokenCredential credential = await armClientFactory.GetCredentialAsync();
    
        _logsClient = new LogsQueryClient(credential);
        _metricsClient = new MetricsQueryClient(credential);
    
        logger.LogInformation("Created new query clients using credential from factory");
    
        return (_logsClient, _metricsClient);
    }

    public async Task<LogQueryResult> QueryLogsAsync(string workspaceId, string query, TimeSpan? timeSpan = null)
    {
        var sw = Stopwatch.StartNew();
        var queryStart = DateTime.UtcNow;
        
        try
        {
            (LogsQueryClient logsClient, _) = await GetQueryClientsAsync();
            
            QueryTimeRange timeRange = timeSpan.HasValue 
                ? new QueryTimeRange(timeSpan.Value) 
                : QueryTimeRange.All;

            Response<LogsQueryResult> response = await logsClient.QueryWorkspaceAsync(
                workspaceId,
                query,
                timeRange);

            if (response.Value.Status == LogsQueryResultStatus.Success)
            {
                var result = new LogQueryResult();
                int totalRows = 0;
                var allMessages = new List<string>();
                
                foreach (LogsTable? table in response.Value.AllTables)
                {
                    var tableDto = new LogTable
                    {
                        Name = table.Name
                    };

                    foreach (LogsTableColumn? column in table.Columns)
                    {
                        tableDto.Columns.Add(new LogColumn
                        {
                            Name = column.Name,
                            Type = column.Type.ToString()
                        });
                    }

                    foreach (LogsTableRow? row in table.Rows)
                    {
                        var rowDict = new Dictionary<string, object?>();
                        for (var i = 0; i < table.Columns.Count; i++)
                        {
                            rowDict[table.Columns[i].Name] = row[i];
                            
                            // Collect message-like columns for format analysis
                            if (table.Columns[i].Name.Contains("Message", StringComparison.OrdinalIgnoreCase) ||
                                table.Columns[i].Name.Contains("Content", StringComparison.OrdinalIgnoreCase))
                            {
                                if (row[i] != null)
                                {
                                    allMessages.Add(row[i].ToString() ?? string.Empty);
                                }
                            }
                        }
                        tableDto.Rows.Add(rowDict);
                        totalRows++;
                    }

                    result.Tables.Add(tableDto);
                }

                sw.Stop();
                
                // Add metadata
                result.Metadata = new QueryMetadata
                {
                    QueryStartTime = queryStart,
                    QueryEndTime = DateTime.UtcNow,
                    DurationMs = sw.ElapsedMilliseconds,
                    TotalRows = totalRows,
                    TotalTables = result.Tables.Count
                };
                
                // Add format analysis if we have message data
                if (allMessages.Count > 0)
                {
                    result.FormatAnalysis = AnalyzeLogFormats(allMessages);
                }

                return result;
            }

            return new LogQueryResult
            {
                Error = $"Query failed with status: {response.Value.Status}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying logs");
            return new LogQueryResult { Error = ex.Message };

        }
    }

    public async Task<IEnumerable<string?>> ListLogGroupsAsync(string? subscriptionId = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var workspaceIds = new List<string>();

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                workspaceIds.AddRange(subscription.GetOperationalInsightsWorkspaces()
                    .Select(workspace => workspace.Data.CustomerId.ToString() ?? string.Empty));
            }
            else
            {
                // List across all subscriptions
                await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                {
                    workspaceIds.AddRange(subscription.GetOperationalInsightsWorkspaces()
                        .Select(workspace => workspace.Data.CustomerId.ToString() ?? string.Empty));
                }
            }

            return workspaceIds;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log groups");
            throw;
        }
    }

    public async Task<IEnumerable<string>> ListLogStreamsAsync(string workspaceId)
    {
        try
        {
            var query = "search * | distinct $table | project TableName = $table";
            LogQueryResult result = await QueryLogsAsync(workspaceId, query, TimeSpan.FromDays(1));

            if (result.Error is not null)
                return [];

            var streams = new List<string>();
            foreach (LogTable table in result.Tables)
            {
                foreach (Dictionary<string, object?> row in table.Rows)
                {
                    if (row.TryGetValue("TableName", out object? value) && value is not null)
                    {
                        streams.Add(value.ToString() ?? "");
                    }
                }
            }

            return streams;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing log streams");
            throw;
        }
    }

    public async Task<List<LogMatch>> SearchLogsWithRegexAsync(string workspaceId, string regexPattern, 
        TimeSpan? timeSpan = null, int contextLines = 3, bool caseSensitive = false, int maxMatches = 100)
    {
        try
        {
            var query = $@"
                search *
                | limit {maxMatches * 2}
                | project TimeGenerated, Message = tostring(pack_all())
                | order by TimeGenerated desc";

            LogQueryResult result = await QueryLogsAsync(workspaceId, query, timeSpan ?? TimeSpan.FromHours(24));
            
            if (result.Error is not null || result.Tables.Count == 0)
                return [];

            var regex = new Regex(regexPattern, 
                caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

            var matches = new List<LogMatch>();
            var allMessages = new List<(DateTime timestamp, string message)>();

            LogTable table = result.Tables[0];
            foreach (Dictionary<string, object?> row in table.Rows)
            {
                if (!row.TryGetValue("TimeGenerated", out object? tsValue) ||
                    !row.TryGetValue("Message", out object? msgValue) ||
                    tsValue is null || msgValue is null) continue;
                DateTime timestamp = tsValue switch
                {
                    DateTimeOffset dto => dto.DateTime,
                    DateTime dt => dt,
                    _ => DateTime.Parse(tsValue.ToString()!)
                };
                allMessages.Add((timestamp, msgValue.ToString() ?? ""));
            }

            for (var i = 0; i < allMessages.Count && matches.Count < maxMatches; i++)
            {
                if (!regex.IsMatch(allMessages[i].message)) continue;
                var match = new LogMatch
                {
                    LogGroup = workspaceId,
                    Timestamp = allMessages[i].timestamp,
                    Message = allMessages[i].message,
                    LineNumber = i + 1
                };

                for (int j = Math.Max(0, i - contextLines); j < i; j++)
                {
                    match.ContextBefore.Add(allMessages[j].message);
                }

                for (int j = i + 1; j < Math.Min(allMessages.Count, i + contextLines + 1); j++)
                {
                    match.ContextAfter.Add(allMessages[j].message);
                }

                matches.Add(match);
            }

            return matches;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching logs with regex");
            throw;
        }
    }

    public async Task<List<LogMatch>> SearchMultipleWorkspacesWithRegexAsync(IEnumerable<string> workspaceIds, 
        string regexPattern, TimeSpan? timeSpan = null, int contextLines = 2, bool caseSensitive = false, 
        int maxMatches = 100, int maxStreamsPerGroup = 5)
    {
        var allMatches = new List<LogMatch>();
        int remainingMatches = maxMatches;

        foreach (string workspaceId in workspaceIds.Take(maxStreamsPerGroup))
        {
            if (remainingMatches <= 0) break;

            try
            {
                List<LogMatch> matches = await SearchLogsWithRegexAsync(
                    workspaceId, 
                    regexPattern, 
                    timeSpan, 
                    contextLines, 
                    caseSensitive, 
                    remainingMatches);

                allMatches.AddRange(matches);
                remainingMatches -= matches.Count;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error searching workspace {WorkspaceId}", workspaceId);
            }
        }

        return allMatches.OrderByDescending(m => m.Timestamp).ToList();
    }

    public async Task<MetricQueryResult> QueryMetricsAsync(string resourceId, IEnumerable<string> metricNames, 
        DateTime startTime, DateTime endTime, TimeSpan? interval = null, IEnumerable<string>? aggregations = null)
    {
        var sw = Stopwatch.StartNew();
        var queryStart = DateTime.UtcNow;
        
        try
        {
            (_, MetricsQueryClient metricsClient) = await GetQueryClientsAsync();
            
            var options = new MetricsQueryOptions
            {
                TimeRange = new QueryTimeRange(startTime, endTime)
            };

            if (interval.HasValue)
            {
                options.Granularity = interval.Value;
            }

            if (aggregations is not null)
            {
                foreach (string agg in aggregations)
                {
                    options.Aggregations.Add(ParseAggregation(agg));
                }
            }

            Response<MetricsQueryResult> response = await metricsClient.QueryResourceAsync(
                resourceId,
                metricNames,
                options);

            var result = new MetricQueryResult
            {
                Namespace = response.Value.Namespace,
                ResourceRegion = response.Value.ResourceRegion
            };
            
            int totalDataPoints = 0;

            foreach (MetricResult? metric in response.Value.Metrics)
            {
                var metricData = new MetricData
                {
                    Name = metric.Name,
                    DisplayName = metric.Name, // Use Name as DisplayName doesn't exist
                    Unit = metric.Unit.ToString()
                };

                foreach (MetricTimeSeriesElement? timeSeries in metric.TimeSeries)
                {
                    var timeSeriesData = new TimeSeriesData();

                    // Note: MetadataValues property doesn't exist in current SDK
                    // Skip metadata collection

                    foreach (Azure.Monitor.Query.Models.MetricValue? value in timeSeries.Values)
                    {
                        timeSeriesData.Data.Add(new MetricValue
                        {
                            TimeStamp = value.TimeStamp.DateTime,
                            Average = value.Average,
                            Minimum = value.Minimum,
                            Maximum = value.Maximum,
                            Total = value.Total,
                            Count = value.Count
                        });
                        totalDataPoints++;
                    }

                    metricData.TimeSeries.Add(timeSeriesData);
                }

                result.Metrics.Add(metricData);
            }
            
            sw.Stop();
            
            // Add metadata
            result.Metadata = new MetricQueryMetadata
            {
                QueryStartTime = queryStart,
                QueryEndTime = DateTime.UtcNow,
                DurationMs = sw.ElapsedMilliseconds,
                TotalMetrics = result.Metrics.Count,
                TotalDataPoints = totalDataPoints,
                Interval = interval
            };

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying metrics");
            return new MetricQueryResult { Error = ex.Message };

        }
    }

    public async Task<IEnumerable<string>> ListMetricsAsync(string resourceId, string? metricNamespace = null)
    {
        try
        {
            // Note: The SDK doesn't expose GetMetricDefinitionsAsync publicly
            // We need to use a workaround by querying with an empty metric list
            // or use the REST API directly
            
            // For now, return a helpful message
            logger.LogWarning("ListMetrics requires direct REST API access or updated SDK");
            
            // Return common metric names as fallback
            return new List<string>
            {
                "Note: Full metric listing requires direct REST API access",
                "Common metrics: Percentage CPU, Memory Working Set, Data In, Data Out"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing metrics");
            throw;
        }
    }

    public async Task<IEnumerable<ApplicationInsightsDto>> ListApplicationInsightsAsync(
        string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var components = new List<ApplicationInsightsDto>();

            if (!string.IsNullOrEmpty(resourceGroupName) && !string.IsNullOrEmpty(subscriptionId))
            {
                ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));
                
                await foreach (ApplicationInsightsComponentResource? component in resourceGroup.GetApplicationInsightsComponents())
                {
                    components.Add(MapToDto(component));
                }
            }
            else if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                components.AddRange(subscription.GetApplicationInsightsComponents().Select(MapToDto));
            }
            else
            {
                // List across all subscriptions
                await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                {
                    components.AddRange(subscription.GetApplicationInsightsComponents().Select(MapToDto));
                }
            }

            return components;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing Application Insights components");
            throw;
        }
    }

    public async Task<ApplicationInsightsDto?> GetApplicationInsightsAsync(
        string resourceGroupName, string componentName, string? subscriptionId = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            
            if (string.IsNullOrEmpty(subscriptionId))
            {
                // Try to find in any subscription
                await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                {
                    try
                    {
                        ResourceGroupResource resourceGroup = await subscription.GetResourceGroups()
                            .GetAsync(resourceGroupName);
                        ApplicationInsightsComponentResource component = await resourceGroup
                            .GetApplicationInsightsComponents()
                            .GetAsync(componentName);
                        return MapToDto(component);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        // Continue searching in other subscriptions
                    }
                }
                return null;
            }

            ResourceGroupResource? targetResourceGroup = armClient.GetResourceGroupResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));
            
            Response<ApplicationInsightsComponentResource> response = await targetResourceGroup
                .GetApplicationInsightsComponents()
                .GetAsync(componentName);

            return MapToDto(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Application Insights component");
            throw;
        }
    }

    public async Task<IEnumerable<AlertRuleDto>> ListAlertsAsync(
        string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var alerts = new List<AlertRuleDto>();

            if (!string.IsNullOrEmpty(resourceGroupName) && !string.IsNullOrEmpty(subscriptionId))
            {
                ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));
                
                await foreach (ScheduledQueryRuleResource? alert in resourceGroup.GetScheduledQueryRules())
                {
                    alerts.Add(MapAlertToDto(alert));
                }
            }
            else if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                alerts.AddRange(subscription.GetScheduledQueryRules().Select(MapAlertToDto));
            }
            else
            {
                // List across all subscriptions
                await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                {
                    alerts.AddRange(subscription.GetScheduledQueryRules().Select(MapAlertToDto));
                }
            }

            return alerts;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing alerts");
            throw;
        }
    }

    public async Task<AlertRuleDto?> CreateAlertAsync(string subscriptionId, string resourceGroupName, 
        string alertName, string description, string severity, string workspaceId, string query, 
        string evaluationFrequency, string windowSize)
    {
        try
        {
            // Note: Creating alerts requires complex ScheduledQueryRuleData construction
            // This is a placeholder implementation
            logger.LogWarning("Alert creation requires additional SDK configuration and is not fully implemented");
            
            return new AlertRuleDto
            {
                Name = alertName,
                Description = description,
                Severity = severity,
                IsEnabled = false
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating alert");
            throw;
        }
    }

    private static ApplicationInsightsDto MapToDto(ApplicationInsightsComponentResource component)
    {
        return new ApplicationInsightsDto
        {
            Id = component.Id.ToString(),
            Name = component.Data.Name,
            Location = component.Data.Location.ToString(),
            ResourceGroup = component.Id.ResourceGroupName ?? "",
            ApplicationType = component.Data.ApplicationType?.ToString() ?? "web",
            ApplicationId = component.Data.AppId,
            InstrumentationKey = component.Data.InstrumentationKey,
            ConnectionString = component.Data.ConnectionString,
            ProvisioningState = component.Data.ProvisioningState,
            Tags = component.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
        };
    }

    private static AlertRuleDto MapAlertToDto(ScheduledQueryRuleResource alert)
    {
        return new AlertRuleDto
        {
            Id = alert.Id.ToString(),
            Name = alert.Data.Name,
            Location = alert.Data.Location.ToString(),
            Description = alert.Data.Description,
            IsEnabled = alert.Data.IsEnabled ?? false,
            Severity = alert.Data.Severity?.ToString() ?? "Unknown",
            EvaluationFrequency = alert.Data.EvaluationFrequency?.ToString(),
            WindowSize = alert.Data.WindowSize?.ToString(),
            Scopes = alert.Data.Scopes?.ToList() ?? [],
            Tags = alert.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
        };
    }

    private static MetricAggregationType ParseAggregation(string aggregation)
    {
        return aggregation.ToLowerInvariant() switch
        {
            "average" => MetricAggregationType.Average,
            "count" => MetricAggregationType.Count,
            "maximum" => MetricAggregationType.Maximum,
            "minimum" => MetricAggregationType.Minimum,
            "total" => MetricAggregationType.Total,
            _ => MetricAggregationType.Average
        };
    }

    /// <summary>
    /// Analyze log message formats to help AI understand log structure
    /// </summary>
    private static LogFormatAnalysis AnalyzeLogFormats(List<string> messages)
    {
        var formatCounts = new Dictionary<string, int>
        {
            ["JSON"] = 0,
            ["KeyValue"] = 0,
            ["PlainText"] = 0
        };

        var jsonSampleKeys = new HashSet<string>();
        var keyValuePatterns = new HashSet<string>();

        // Sample up to 100 messages for analysis
        var sampleSize = Math.Min(100, messages.Count);
        
        for (int i = 0; i < sampleSize; i++)
        {
            string msg = messages[i];
            if (string.IsNullOrWhiteSpace(msg)) continue;

            // Check for JSON format
            if (IsJsonFormat(msg, jsonSampleKeys))
            {
                formatCounts["JSON"]++;
            }
            // Check for key-value format (key=value or key:value patterns)
            else if (IsKeyValueFormat(msg, keyValuePatterns))
            {
                formatCounts["KeyValue"]++;
            }
            else
            {
                formatCounts["PlainText"]++;
            }
        }

        // Determine dominant format
        var dominantEntry = formatCounts.OrderByDescending(kvp => kvp.Value).First();
        var totalMessages = formatCounts.Values.Sum();
        var dominantPercentage = totalMessages > 0 
            ? (double)dominantEntry.Value / totalMessages * 100 
            : 0;

        return new LogFormatAnalysis
        {
            FormatCounts = formatCounts,
            DominantFormat = dominantEntry.Key,
            DominantFormatPercentage = Math.Round(dominantPercentage, 2),
            JsonSampleKeys = jsonSampleKeys.Count > 0 
                ? jsonSampleKeys.Take(20).ToList() 
                : null,
            KeyValuePatterns = keyValuePatterns.Count > 0 
                ? keyValuePatterns.Take(20).ToList() 
                : null
        };
    }

    private static bool IsJsonFormat(string message, HashSet<string> sampleKeys)
    {
        if (!message.TrimStart().StartsWith("{") && !message.TrimStart().StartsWith("["))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(message);
            
            // Collect sample keys from JSON objects
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (sampleKeys.Count < 20)
                    {
                        sampleKeys.Add(prop.Name);
                    }
                }
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKeyValueFormat(string message, HashSet<string> patterns)
    {
        // Look for common key-value patterns
        var kvRegex = new Regex(@"(\w+)\s*[:=]\s*([^\s,;]+)", RegexOptions.Compiled);
        var matches = kvRegex.Matches(message);

        if (matches.Count >= 2) // Need at least 2 key-value pairs to consider it structured
        {
            foreach (Match match in matches.Cast<Match>().Take(5))
            {
                if (patterns.Count < 20)
                {
                    patterns.Add(match.Groups[1].Value); // Add the key name
                }
            }
            return true;
        }

        return false;
    }

}