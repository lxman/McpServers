namespace PlaywrightServerMcp.Models;

/// <summary>
/// Lazy loading analysis
/// </summary>
public class LazyLoadingAnalysis
{
    public bool Implemented { get; set; }
    public int LazyModuleCount { get; set; }
    public List<string> LazyRoutes { get; set; } = [];
    public List<string> Opportunities { get; set; } = [];
}