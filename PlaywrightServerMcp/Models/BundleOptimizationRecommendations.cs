namespace PlaywrightServerMcp.Models;

/// <summary>
/// Bundle optimization recommendations
/// </summary>
public class BundleOptimizationRecommendations
{
    public List<OptimizationRecommendation> HighPriority { get; set; } = [];
    public List<OptimizationRecommendation> MediumPriority { get; set; } = [];
    public List<OptimizationRecommendation> LowPriority { get; set; } = [];
    public List<OptimizationRecommendation> QuickWins { get; set; } = [];
    public OptimizationSummary Summary { get; set; } = new();
    public ImplementationGuide Implementation { get; set; } = new();
}