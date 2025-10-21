namespace PlaywrightServerMcp.Models;

/// <summary>
/// Dependency optimization analysis
/// </summary>
public class DependencyOptimization
{
    public int TotalDependencies { get; set; }
    public int OptimizedDependencies { get; set; }
    public int UnusedDependencies { get; set; }
    public long PotentialSavings { get; set; }
    public double OptimizationScore { get; set; } // 0-100
    public List<string> QuickWins { get; set; } = [];
}