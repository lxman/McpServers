namespace PlaywrightServerMcp.Models;

/// <summary>
/// Contract improvement recommendations
/// </summary>
public class ContractRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Implementation { get; set; } = string.Empty;
    public string ExpectedBenefit { get; set; } = string.Empty;
    public int EstimatedEffort { get; set; } // hours
}