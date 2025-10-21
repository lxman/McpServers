namespace PlaywrightServerMcp.Models;

/// <summary>
/// Dependency analysis for bundle optimization
/// </summary>
public class BundleSizeDependencyAnalysis
{
    public List<DependencyImpact> ThirdPartyDependencies { get; set; } = [];
    public List<DependencyImpact> InternalDependencies { get; set; } = [];
    public DependencyOptimization Optimization { get; set; } = new();
    public List<string> OptimizationOpportunities { get; set; } = [];
    public UnusedDependencies UnusedDependencies { get; set; } = new();
}