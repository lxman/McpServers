namespace PlaywrightServerMcp.Models;

/// <summary>
/// Module optimization analysis
/// </summary>
public class ModuleOptimization
{
    public bool IsLazyLoaded { get; set; }
    public bool HasSharedComponents { get; set; }
    public bool HasCircularDependencies { get; set; }
    public bool ProperlyTreeShaken { get; set; }
    public double OptimizationScore { get; set; } // 0-100
    public List<string> ModuleDependencies { get; set; } = [];
    public List<string> UnusedExports { get; set; } = [];
}