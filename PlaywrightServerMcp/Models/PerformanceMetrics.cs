namespace PlaywrightServerMcp.Models;

/// <summary>
/// Performance metrics related to bundle size
/// </summary>
public class PerformanceMetrics
{
    public LoadingMetrics Loading { get; set; } = new();
    public RenderingMetrics Rendering { get; set; } = new();
    public NetworkMetrics Network { get; set; } = new();
    public UserExperienceMetrics UserExperience { get; set; } = new();
}