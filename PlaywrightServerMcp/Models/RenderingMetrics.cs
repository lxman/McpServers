namespace PlaywrightServerMcp.Models;

/// <summary>
/// Rendering performance metrics
/// </summary>
public class RenderingMetrics
{
    public double EstimatedFirstContentfulPaint { get; set; }
    public double EstimatedLargestContentfulPaint { get; set; }
    public double EstimatedTimeToInteractive { get; set; }
    public double EstimatedFirstInputDelay { get; set; }
}