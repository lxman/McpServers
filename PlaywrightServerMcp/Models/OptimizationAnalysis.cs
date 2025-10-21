namespace PlaywrightServerMcp.Models;

/// <summary>
/// Optimization analysis
/// </summary>
public class OptimizationAnalysis
{
    public bool TreeShakingEnabled { get; set; }
    public bool AotEnabled { get; set; }
    public bool MinificationEnabled { get; set; }
    public bool CompressionEnabled { get; set; }
    public bool LazyLoadingSupported { get; set; }
    public List<string> OptimizationOpportunities { get; set; } = [];
}