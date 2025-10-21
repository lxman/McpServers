namespace PlaywrightServerMcp.Models;

/// <summary>
/// Budget violation details
/// </summary>
public class BudgetViolation
{
    public string BudgetType { get; set; } = string.Empty;
    public long ExpectedSize { get; set; }
    public long ActualSize { get; set; }
    public long Excess { get; set; }
    public string Severity { get; set; } = string.Empty;
    public List<string> CausingComponents { get; set; } = [];
}