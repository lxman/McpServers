namespace PlaywrightServerMcp.Models;

/// <summary>
/// Growth potential analysis
/// </summary>
public class GrowthPotential
{
    public string TeamSize { get; set; } = string.Empty; // small, medium, large, enterprise
    public string Complexity { get; set; } = string.Empty; // simple, moderate, complex, enterprise
    public List<string> ScalingBottlenecks { get; set; } = [];
}