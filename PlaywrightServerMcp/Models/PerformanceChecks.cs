namespace PlaywrightServerMcp.Models;

/// <summary>
/// Performance-related checks
/// </summary>
public class PerformanceChecks
{
    public bool BundleOptimization { get; set; }
    public bool LazyLoading { get; set; }
    public bool TreeShaking { get; set; }
    public bool SourceMaps { get; set; }
    public List<string> Recommendations { get; set; } = [];
}