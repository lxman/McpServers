namespace PlaywrightServerMcp.Models;

/// <summary>
/// Optimization summary
/// </summary>
public class OptimizationSummary
{
    public long TotalPotentialSavings { get; set; }
    public double TotalSavingsPercentage { get; set; }
    public int TotalRecommendations { get; set; }
    public int QuickWinCount { get; set; }
    public string EstimatedImplementationTime { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
    public List<string> PrimaryFocusAreas { get; set; } = [];
}