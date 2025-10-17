using AzureServer.Services.CostManagement;
using AzureServer.Services.CostManagement.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CostManagementController(ICostManagementService costService, ILogger<CostManagementController> logger) : ControllerBase
{
    [HttpGet("current-month")]
    public async Task<ActionResult> GetCurrentMonthCosts([FromQuery] string? subscriptionId = null)
    {
        try
        {
            var result = await costService.GetCurrentMonthCostsAsync(subscriptionId);
            return Ok(new { success = true, costs = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current month costs");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetCurrentMonthCosts", type = ex.GetType().Name });
        }
    }

    [HttpGet("period")]
    public async Task<ActionResult> GetCostsForPeriod(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetCostsForPeriodAsync(start, end, subscriptionId);
            return Ok(new { success = true, costs = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs for period {StartDate} to {EndDate}", startDate, endDate);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetCostsForPeriod", type = ex.GetType().Name });
        }
    }

    [HttpGet("by-service")]
    public async Task<ActionResult> GetCostsByService(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetCostsByServiceAsync(start, end, subscriptionId);
            return Ok(new { success = true, costs = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs by service");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetCostsByService", type = ex.GetType().Name });
        }
    }

    [HttpGet("by-resource-group")]
    public async Task<ActionResult> GetCostsByResourceGroup(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetCostsByResourceGroupAsync(start, end, subscriptionId);
            return Ok(new { success = true, costs = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting costs by resource group");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetCostsByResourceGroup", type = ex.GetType().Name });
        }
    }

    [HttpGet("daily")]
    public async Task<ActionResult> GetDailyCosts(
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);
            var result = await costService.GetDailyCostsAsync(start, end, subscriptionId);
            return Ok(new { success = true, costs = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting daily costs");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetDailyCosts", type = ex.GetType().Name });
        }
    }

    [HttpGet("forecast")]
    public async Task<ActionResult> GetCostForecast(
        [FromQuery] int daysAhead = 30,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var result = await costService.GetCostForecastAsync(daysAhead, subscriptionId);
            return Ok(new { success = true, forecast = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cost forecast");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetCostForecast", type = ex.GetType().Name });
        }
    }

    [HttpGet("budgets")]
    public async Task<ActionResult> GetBudgets([FromQuery] string? subscriptionId = null)
    {
        try
        {
            var budgets = await costService.GetBudgetsAsync(subscriptionId);
            return Ok(new { success = true, budgets = budgets.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting budgets");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBudgets", type = ex.GetType().Name });
        }
    }

    [HttpGet("budgets/{budgetName}")]
    public async Task<ActionResult> GetBudget(string budgetName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var budget = await costService.GetBudgetAsync(budgetName, subscriptionId);
            if (budget is null)
                return NotFound(new { success = false, error = $"Budget {budgetName} not found" });

            return Ok(new { success = true, budget });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting budget {BudgetName}", budgetName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBudget", type = ex.GetType().Name });
        }
    }
}