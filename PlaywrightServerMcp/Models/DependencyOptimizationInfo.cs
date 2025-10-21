namespace PlaywrightServerMcp.Models;

/// <summary>
/// Dependency optimization information
/// </summary>
public class DependencyOptimizationInfo
{
    public bool CanBeTreeShaken { get; set; }
    public bool CanBeLazyLoaded { get; set; }
    public bool HasSmallerAlternatives { get; set; }
    public List<string> OptimizationSuggestions { get; set; } = [];
    public List<AlternativeDependency> AlternativeDependencies { get; set; } = [];
}