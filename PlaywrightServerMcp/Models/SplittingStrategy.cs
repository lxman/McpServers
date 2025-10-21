namespace PlaywrightServerMcp.Models;

/// <summary>
/// Recommended chunk splitting strategy
/// </summary>
public class SplittingStrategy
{
    public string Strategy { get; set; } = string.Empty; // feature-based, route-based, vendor-splitting
    public List<string> RecommendedSplits { get; set; } = [];
    public List<string> MergeOpportunities { get; set; } = [];
    public string Rationale { get; set; } = string.Empty;
}