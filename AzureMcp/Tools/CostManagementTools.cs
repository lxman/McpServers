using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.CostManagement;
using AzureMcp.Services.CostManagement.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class CostManagementTools(ICostManagementService costService)
{
    [McpServerTool]
    [Description("Get cost summary for the current month")]
    public async Task<string> GetCurrentMonthCostsAsync(
        [Description("Optional subscription ID (if not provided, uses default subscription)")]
        string? subscriptionId = null)
    {
        try
        {
            CostQueryResult result = await costService.GetCurrentMonthCostsAsync(subscriptionId);
            return JsonSerializer.Serialize(new { success = true, costs = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetCurrentMonthCosts");
        }
    }

    [McpServerTool]
    [Description("Get cost summary for a specific time period")]
    public async Task<string> GetCostsForPeriodAsync(
        [Description("Start date (ISO 8601 format, e.g., '2025-09-01')")]
        string startDate,
        [Description("End date (ISO 8601 format, e.g., '2025-09-30')")]
        string endDate,
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            DateTime start = DateTime.Parse(startDate);
            DateTime end = DateTime.Parse(endDate);

            CostQueryResult result = await costService.GetCostsForPeriodAsync(start, end, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, costs = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetCostsForPeriod");
        }
    }

    [McpServerTool]
    [Description("Get costs grouped by Azure service (e.g., App Service, Storage, etc.)")]
    public async Task<string> GetCostsByServiceAsync(
        [Description("Start date (ISO 8601 format)")]
        string startDate,
        [Description("End date (ISO 8601 format)")]
        string endDate,
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            DateTime start = DateTime.Parse(startDate);
            DateTime end = DateTime.Parse(endDate);

            CostQueryResult result = await costService.GetCostsByServiceAsync(start, end, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, costs = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetCostsByService");
        }
    }

    [McpServerTool]
    [Description("Get costs grouped by resource group")]
    public async Task<string> GetCostsByResourceGroupAsync(
        [Description("Start date (ISO 8601 format)")]
        string startDate,
        [Description("End date (ISO 8601 format)")]
        string endDate,
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            DateTime start = DateTime.Parse(startDate);
            DateTime end = DateTime.Parse(endDate);

            CostQueryResult result = await costService.GetCostsByResourceGroupAsync(start, end, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, costs = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetCostsByResourceGroup");
        }
    }

    [McpServerTool]
    [Description("Get daily cost breakdown for a period")]
    public async Task<string> GetDailyCostsAsync(
        [Description("Start date (ISO 8601 format)")]
        string startDate,
        [Description("End date (ISO 8601 format)")]
        string endDate,
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            DateTime start = DateTime.Parse(startDate);
            DateTime end = DateTime.Parse(endDate);

            CostQueryResult result = await costService.GetDailyCostsAsync(start, end, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, costs = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetDailyCosts");
        }
    }

    [McpServerTool]
    [Description("Get cost forecast for the next N days")]
    public async Task<string> GetCostForecastAsync(
        [Description("Number of days ahead to forecast (default: 30)")]
        int daysAhead = 30,
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            CostQueryResult result = await costService.GetCostForecastAsync(daysAhead, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, forecast = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetCostForecast");
        }
    }

    [McpServerTool]
    [Description("Get all budgets configured for the subscription")]
    public async Task<string> GetBudgetsAsync(
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            IEnumerable<BudgetDto> budgets = await costService.GetBudgetsAsync(subscriptionId);
            return JsonSerializer.Serialize(new { success = true, budgets = budgets.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBudgets");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific budget")]
    public async Task<string> GetBudgetAsync(
        [Description("Budget name")]
        string budgetName,
        [Description("Optional subscription ID")]
        string? subscriptionId = null)
    {
        try
        {
            BudgetDto? budget = await costService.GetBudgetAsync(budgetName, subscriptionId);
            if (budget == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Budget {budgetName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, budget },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBudget");
        }
    }

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = ex.Message,
            operation,
            type = ex.GetType().Name,
            stackTrace = ex.StackTrace
        }, SerializerOptions.JsonOptionsIndented);
    }
}
