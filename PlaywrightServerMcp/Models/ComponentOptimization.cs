namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component optimization analysis
/// </summary>
public class ComponentOptimization
{
    public bool OnPushStrategy { get; set; }
    public bool LazyLoaded { get; set; }
    public bool TreeShakable { get; set; }
    public bool HasDeadCode { get; set; }
    public bool UsesStandaloneAPI { get; set; }
    public double OptimizationScore { get; set; } // 0-100
    public List<string> OptimizationSuggestions { get; set; } = [];
}