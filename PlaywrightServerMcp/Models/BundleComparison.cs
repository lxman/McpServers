namespace PlaywrightServerMcp.Models;

/// <summary>
/// Bundle comparison against recommended sizes
/// </summary>
public class BundleComparison
{
    public BudgetStatus InitialBudgetStatus { get; set; } = new();
    public BudgetStatus AnyBudgetStatus { get; set; } = new();
    public List<BudgetViolation> BudgetViolations { get; set; } = [];
    public RecommendedSizes RecommendedSizes { get; set; } = new();
    public SizeScore Score { get; set; } = new();
}