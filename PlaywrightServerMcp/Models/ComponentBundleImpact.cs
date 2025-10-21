namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component-specific bundle impact analysis
/// </summary>
public class ComponentBundleImpact
{
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentPath { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty; // standalone, module-based
    public long SizeBytes { get; set; }
    public long GzippedSizeBytes { get; set; }
    public double PercentageOfBundle { get; set; }
    public int DependencyCount { get; set; }
    public List<string> Dependencies { get; set; } = [];
    public List<ComponentDependency> ComponentDependencies { get; set; } = [];
    public ComponentOptimization Optimization { get; set; } = new();
    public ComponentComplexity Complexity { get; set; } = new();
    public List<string> OptimizationOpportunities { get; set; } = [];
    public string ImpactLevel { get; set; } = string.Empty; // low, medium, high, critical
}