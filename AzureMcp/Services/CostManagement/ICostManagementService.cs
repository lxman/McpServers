using AzureMcp.Services.CostManagement.Models;

namespace AzureMcp.Services.CostManagement;

public interface ICostManagementService
{
    /// <summary>
    /// Get cost summary for current month
    /// </summary>
    Task<CostQueryResult> GetCurrentMonthCostsAsync(string? subscriptionId = null);
    
    /// <summary>
    /// Get cost summary for a specific time period
    /// </summary>
    Task<CostQueryResult> GetCostsForPeriodAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null);
    
    /// <summary>
    /// Get costs grouped by service
    /// </summary>
    Task<CostQueryResult> GetCostsByServiceAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null);
    
    /// <summary>
    /// Get costs grouped by resource group
    /// </summary>
    Task<CostQueryResult> GetCostsByResourceGroupAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null);
    
    /// <summary>
    /// Get daily cost breakdown
    /// </summary>
    Task<CostQueryResult> GetDailyCostsAsync(DateTime startDate, DateTime endDate, string? subscriptionId = null);
    
    /// <summary>
    /// Get cost forecast for next period
    /// </summary>
    Task<CostQueryResult> GetCostForecastAsync(int daysAhead = 30, string? subscriptionId = null);
    
    /// <summary>
    /// Get all budgets
    /// </summary>
    Task<IEnumerable<BudgetDto>> GetBudgetsAsync(string? subscriptionId = null);
    
    /// <summary>
    /// Get specific budget
    /// </summary>
    Task<BudgetDto?> GetBudgetAsync(string budgetName, string? subscriptionId = null);
}
