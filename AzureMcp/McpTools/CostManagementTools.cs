using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.CostManagement;
using AzureServer.Core.Services.CostManagement.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Cost Management operations
/// </summary>
[McpServerToolType]
public class CostManagementTools(
    ICostManagementService costService,
    ILogger<CostManagementTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("get_current_month_costs")]
    [Description("Get current month costs. See skills/azure/costmanagement/get-current-month-costs.md only when using this tool")]
    public async Task<string> GetCurrentMonthCosts(string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting current month costs");
            var result = await costService.GetCurrentMonthCostsAsync(subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                costs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current month costs");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetCurrentMonthCosts",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_costs_for_period")]
    [Description("Get costs for specific period. See skills/azure/costmanagement/get-costs-for-period.md only when using this tool")]
    public async Task<string> GetCostsForPeriod(
        string startDate,
        string endDate,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting costs for period {StartDate} to {EndDate}", startDate, endDate);

            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetCostsForPeriodAsync(start, end, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                costs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs for period {StartDate} to {EndDate}", startDate, endDate);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetCostsForPeriod",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_costs_by_service")]
    [Description("Get costs grouped by service. See skills/azure/costmanagement/get-costs-by-service.md only when using this tool")]
    public async Task<string> GetCostsByService(
        string startDate,
        string endDate,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting costs by service");

            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetCostsByServiceAsync(start, end, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                costs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs by service");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetCostsByService",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_costs_by_resource_group")]
    [Description("Get costs grouped by resource group. See skills/azure/costmanagement/get-costs-by-resource-group.md only when using this tool")]
    public async Task<string> GetCostsByResourceGroup(
        string startDate,
        string endDate,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting costs by resource group");

            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetCostsByResourceGroupAsync(start, end, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                costs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs by resource group");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetCostsByResourceGroup",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_daily_costs")]
    [Description("Get daily costs breakdown. See skills/azure/costmanagement/get-daily-costs.md only when using this tool")]
    public async Task<string> GetDailyCosts(
        string startDate,
        string endDate,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting daily costs");

            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetDailyCostsAsync(start, end, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                costs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting daily costs");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetDailyCosts",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_cost_forecast")]
    [Description("Get cost forecast. See skills/azure/costmanagement/get-cost-forecast.md only when using this tool")]
    public async Task<string> GetCostForecast(int daysAhead = 30, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting cost forecast");
            var result = await costService.GetCostForecastAsync(daysAhead, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                forecast = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cost forecast");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetCostForecast",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_budgets")]
    [Description("Get budgets. See skills/azure/costmanagement/get-budgets.md only when using this tool")]
    public async Task<string> GetBudgets(string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting budgets");
            var budgets = await costService.GetBudgetsAsync(subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                budgets = budgets.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting budgets");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetBudgets",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_budget")]
    [Description("Get budget details. See skills/azure/costmanagement/get-budget.md only when using this tool")]
    public async Task<string> GetBudget(string budgetName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting budget {BudgetName}", budgetName);
            var budget = await costService.GetBudgetAsync(budgetName, subscriptionId);

            if (budget is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Budget {budgetName} not found"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                budget
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting budget {BudgetName}", budgetName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetBudget",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }
}
