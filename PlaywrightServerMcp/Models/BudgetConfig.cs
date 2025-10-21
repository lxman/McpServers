namespace PlaywrightServerMcp.Models;

/// <summary>
/// Budget configuration for bundle size limits
/// </summary>
public class BudgetConfig
{
    public string Type { get; set; } = string.Empty;
    public string Baseline { get; set; } = string.Empty;
    public string Maximum { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}