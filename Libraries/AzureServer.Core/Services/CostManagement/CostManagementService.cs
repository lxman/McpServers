using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Consumption;
using Azure.ResourceManager.CostManagement;
using Azure.ResourceManager.CostManagement.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.CostManagement.Models;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.CostManagement;

/// <summary>
/// Service for Azure Cost Management operations using the official SDK
/// </summary>
public class CostManagementService(
    ArmClientFactory armClientFactory,
    ILogger<CostManagementService> logger)
    : ICostManagementService
{
    public async Task<CostQueryResult> GetCurrentMonthCostsAsync(string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var dataset = new QueryDataset { Granularity = GranularityType.Daily };
            dataset.Aggregation.Add("totalCost", new QueryAggregation("Cost", FunctionType.Sum));
            
            var queryDefinition = new QueryDefinition(ExportType.ActualCost, TimeframeType.MonthToDate, dataset);
            Response<QueryResult>? response = await armClient.UsageQueryAsync(subscription.Id, queryDefinition);
            
            return MapQueryResult(response.Value, "Month to Date");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current month costs");
            throw;
        }
    }

    public async Task<CostQueryResult> GetCostsForPeriodAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var dataset = new QueryDataset { Granularity = GranularityType.Daily };
            dataset.Aggregation.Add("totalCost", new QueryAggregation("Cost", FunctionType.Sum));
            
            var queryDefinition = new QueryDefinition(ExportType.ActualCost, TimeframeType.Custom, dataset)
            {
                TimePeriod = new QueryTimePeriod(startDate, endDate)
            };
            
            Response<QueryResult>? response = await armClient.UsageQueryAsync(subscription.Id, queryDefinition);
            return MapQueryResult(response.Value, $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs for period {StartDate} to {EndDate}", startDate, endDate);
            throw;
        }
    }

    public async Task<CostQueryResult> GetCostsByServiceAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var dataset = new QueryDataset { Granularity = null };  // No granularity for grouping
            dataset.Aggregation.Add("totalCost", new QueryAggregation("Cost", FunctionType.Sum));
            dataset.Grouping.Add(new QueryGrouping(QueryColumnType.Dimension, "ServiceName"));
            
            var queryDefinition = new QueryDefinition(ExportType.ActualCost, TimeframeType.Custom, dataset)
            {
                TimePeriod = new QueryTimePeriod(startDate, endDate)
            };
            
            Response<QueryResult>? response = await armClient.UsageQueryAsync(subscription.Id, queryDefinition);
            return MapQueryResult(response.Value, $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs by service");
            throw;
        }
    }

    public async Task<CostQueryResult> GetCostsByResourceGroupAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var dataset = new QueryDataset { Granularity = null };  // No granularity for grouping
            dataset.Aggregation.Add("totalCost", new QueryAggregation("Cost", FunctionType.Sum));
            dataset.Grouping.Add(new QueryGrouping(QueryColumnType.Dimension, "ResourceGroup"));
            
            var queryDefinition = new QueryDefinition(ExportType.ActualCost, TimeframeType.Custom, dataset)
            {
                TimePeriod = new QueryTimePeriod(startDate, endDate)
            };
            
            Response<QueryResult>? response = await armClient.UsageQueryAsync(subscription.Id, queryDefinition);
            return MapQueryResult(response.Value, $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs by resource group");
            throw;
        }
    }

    public async Task<CostQueryResult> GetDailyCostsAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var dataset = new QueryDataset { Granularity = GranularityType.Daily };
            dataset.Aggregation.Add("totalCost", new QueryAggregation("Cost", FunctionType.Sum));
            
            var queryDefinition = new QueryDefinition(ExportType.ActualCost, TimeframeType.Custom, dataset)
            {
                TimePeriod = new QueryTimePeriod(startDate, endDate)
            };
            
            Response<QueryResult>? response = await armClient.UsageQueryAsync(subscription.Id, queryDefinition);
            return MapQueryResult(response.Value, $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting daily costs");
            throw;
        }
    }

    public async Task<CostQueryResult> GetCostForecastAsync(int daysAhead = 30, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var startDate = DateTime.UtcNow.Date;
            var endDate = startDate.AddDays(daysAhead);
            
            var aggregations = new Dictionary<string, ForecastAggregation>
            {
                ["totalCost"] = new ForecastAggregation("Cost", FunctionType.Sum)
            };
            var dataset = new ForecastDataset(aggregations) 
            { 
                Granularity = GranularityType.Daily 
            };
            
            var forecastDefinition = new ForecastDefinition(ForecastType.Usage, ForecastTimeframe.Custom, dataset)
            {
                TimePeriod = new ForecastTimePeriod(startDate, endDate),
                IncludeActualCost = false,
                IncludeFreshPartialCost = false
            };
            
            Response<ForecastResult> response = await armClient.UsageForecastAsync(subscription.Id, forecastDefinition);
            return MapForecastResult(response.Value, $"Forecast {daysAhead} days");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cost forecast for {DaysAhead} days", daysAhead);
            throw;
        }
    }

    public async Task<IEnumerable<BudgetDto>> GetBudgetsAsync(string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var budgets = armClient.GetConsumptionBudgets(subscription.Id);
            var result = new List<BudgetDto>();
            
            await foreach (var budget in budgets)
            {
                result.Add(MapToBudgetDto(budget.Data));
            }
            
            logger.LogInformation("Retrieved {Count} budgets for subscription", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting budgets for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task<BudgetDto?> GetBudgetAsync(string budgetName, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var subscription = await GetSubscriptionAsync(armClient, subscriptionId);
            
            var budgets = armClient.GetConsumptionBudgets(subscription.Id);
            
            try
            {
                Response<ConsumptionBudgetResource> budget = await budgets.GetAsync(budgetName);
                logger.LogInformation("Retrieved budget {BudgetName}", budgetName);
                return MapToBudgetDto(budget.Value.Data);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                logger.LogWarning("Budget {BudgetName} not found", budgetName);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting budget {BudgetName}", budgetName);
            throw;
        }
    }

    private CostQueryResult MapQueryResult(QueryResult result, string timeFrame)
    {
        var costResult = new CostQueryResult { TimeFrame = timeFrame };

        if (result.Columns is null || result.Rows is null)
        {
            logger.LogWarning("Query result has no columns or rows");
            return costResult;
        }

        int costIdx = -1, dateIdx = -1, serviceIdx = -1, rgIdx = -1, currIdx = -1;

        for (var i = 0; i < result.Columns.Count; i++)
        {
            var name = result.Columns[i].Name?.ToLowerInvariant() ?? "";
            if (name.Contains("cost") || name == "pretaxcost") costIdx = i;
            else if (name.Contains("date")) dateIdx = i;
            else if (name.Contains("service")) serviceIdx = i;
            else if (name.Contains("resourcegroup")) rgIdx = i;
            else if (name.Contains("currency")) currIdx = i;
        }

        decimal totalCost = 0;
        var currency = "USD";

        foreach (IList<BinaryData>? row in result.Rows)
        {
            if (row.Count == 0) continue;

            decimal cost = 0;
            if (costIdx >= 0 && costIdx < row.Count)
            {
                // QueryResult rows contain BinaryData - convert ToString() then parse
                var costStr = row[costIdx].ToString();
                decimal.TryParse(costStr, out cost);
            }

            totalCost += cost;

            if (currIdx >= 0 && currIdx < row.Count)
                currency = row[currIdx].ToString() ?? "USD";

            if (dateIdx >= 0 && dateIdx < row.Count)
            {
                // BinaryData can contain date as string or int (yyyyMMdd format)
                var dateStr = row[dateIdx].ToString();
                DateTime date;
                
                if (int.TryParse(dateStr, out var dateInt) && dateStr?.Length == 8)
                    date = ParseDateInt(dateInt);
                else if (!DateTime.TryParse(dateStr, out date))
                    date = DateTime.UtcNow;

                costResult.CostsByDate.Add(new CostByDate { Date = date, Cost = cost });
            }

            if (serviceIdx >= 0 && serviceIdx < row.Count)
            {
                costResult.CostsByService.Add(new CostByService
                {
                    ServiceName = row[serviceIdx].ToString() ?? "Unknown",
                    Cost = cost,
                    Category = string.Empty
                });
            }

            if (rgIdx >= 0 && rgIdx < row.Count)
            {
                costResult.CostsByResourceGroup.Add(new CostByResourceGroup
                {
                    ResourceGroupName = row[rgIdx].ToString() ?? "Unknown",
                    Cost = cost
                });
            }
        }

        costResult.TotalCost = totalCost;
        costResult.Currency = currency;
        return costResult;
    }

    private static DateTime ParseDateInt(int dateInt)
    {
        var dateStr = dateInt.ToString();
        if (dateStr.Length == 8)
        {
            return new DateTime(int.Parse(dateStr.Substring(0, 4)),
                              int.Parse(dateStr.Substring(4, 2)),
                              int.Parse(dateStr.Substring(6, 2)));
        }
        return DateTime.UtcNow;
    }

    private CostQueryResult MapForecastResult(ForecastResult result, string timeFrame)
    {
        var costResult = new CostQueryResult { TimeFrame = timeFrame };

        if (result.Columns is null || result.Rows is null)
        {
            logger.LogWarning("Forecast result has no columns or rows");
            return costResult;
        }

        int costIdx = -1, dateIdx = -1, currIdx = -1;

        for (var i = 0; i < result.Columns.Count; i++)
        {
            var name = result.Columns[i].Name?.ToLowerInvariant() ?? "";
            if (name.Contains("cost") || name == "pretaxcost") costIdx = i;
            else if (name.Contains("date")) dateIdx = i;
            else if (name.Contains("currency")) currIdx = i;
        }

        decimal totalCost = 0;
        var currency = "USD";

        foreach (IList<BinaryData>? row in result.Rows)
        {
            if (row.Count == 0) continue;

            decimal cost = 0;
            if (costIdx >= 0 && costIdx < row.Count)
            {
                var costStr = row[costIdx].ToString();
                decimal.TryParse(costStr, out cost);
            }

            totalCost += cost;

            if (currIdx >= 0 && currIdx < row.Count)
                currency = row[currIdx].ToString() ?? "USD";

            if (dateIdx < 0 || dateIdx >= row.Count) continue;
            var dateStr = row[dateIdx].ToString();
            DateTime date;
                
            if (int.TryParse(dateStr, out var dateInt) && dateStr?.Length == 8)
                date = ParseDateInt(dateInt);
            else if (!DateTime.TryParse(dateStr, out date))
                date = DateTime.UtcNow;

            costResult.CostsByDate.Add(new CostByDate { Date = date, Cost = cost });
        }

        costResult.TotalCost = totalCost;
        costResult.Currency = currency;
        return costResult;
    }

    private async Task<SubscriptionResource> GetSubscriptionAsync(ArmClient armClient, string? subscriptionId)
    {
        if (!string.IsNullOrEmpty(subscriptionId))
            return armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

        await foreach (var subscription in armClient.GetSubscriptions())
            return subscription;

        throw new InvalidOperationException("No subscriptions available");
    }

    private static BudgetDto MapToBudgetDto(ConsumptionBudgetData data)
    {
        return new BudgetDto
        {
            Id = data.Id?.ToString() ?? string.Empty,
            Name = data.Name ?? string.Empty,
            Amount = data.Amount ?? 0,
            TimeGrain = data.TimeGrain?.ToString() ?? string.Empty,
            StartDate = data.TimePeriod?.StartOn.DateTime ?? DateTime.MinValue,
            EndDate = data.TimePeriod?.EndOn?.DateTime,
            CurrentSpend = data.CurrentSpend?.Amount ?? 0,
            ForecastedSpend = data.ForecastSpend?.Amount ?? 0,
            Currency = data.CurrentSpend?.Unit ?? "USD",
            Notifications = data.Notifications?.Select(n => new BudgetNotification
            {
                Name = n.Key,
                Enabled = n.Value.IsEnabled,
                Threshold = n.Value.Threshold,
                ThresholdType = n.Value.Operator.ToString()
            }).ToList() ?? []
        };
    }
}
