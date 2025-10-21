namespace PlaywrightServerMcp.Models;

/// <summary>
/// Budget status for specific bundle types
/// </summary>
public class BudgetStatus
{
    public bool WithinBudget { get; set; }
    public long MaximumSize { get; set; }
    public long CurrentSize { get; set; }
    public double UtilizationPercentage { get; set; }
    public string Status { get; set; } = string.Empty; // ok, warning, error
}