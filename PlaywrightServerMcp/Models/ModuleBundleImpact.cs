namespace PlaywrightServerMcp.Models;

/// <summary>
/// Module-specific bundle impact analysis
/// </summary>
public class ModuleBundleImpact
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModulePath { get; set; } = string.Empty;
    public string ModuleType { get; set; } = string.Empty; // feature, shared, core, lazy
    public long SizeBytes { get; set; }
    public long GzippedSizeBytes { get; set; }
    public double PercentageOfBundle { get; set; }
    public int ComponentCount { get; set; }
    public int ServiceCount { get; set; }
    public List<string> Components { get; set; } = [];
    public List<string> Services { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
    public ModuleOptimization Optimization { get; set; } = new();
    public string LoadingStrategy { get; set; } = string.Empty; // eager, lazy
    public List<string> OptimizationOpportunities { get; set; } = [];
}