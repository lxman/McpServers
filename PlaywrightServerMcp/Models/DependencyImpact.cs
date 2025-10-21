namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual dependency impact on bundle size
/// </summary>
public class DependencyImpact
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long GzippedSizeBytes { get; set; }
    public double PercentageOfBundle { get; set; }
    public bool IsTreeShakable { get; set; }
    public bool IsUsed { get; set; }
    public List<string> UsedBy { get; set; } = [];
    public List<string> Alternatives { get; set; } = [];
    public DependencyOptimizationInfo Optimization { get; set; } = new();
}