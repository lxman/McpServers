namespace PlaywrightServerMcp.Models;

/// <summary>
/// Coverage thresholds configuration
/// </summary>
public class CoverageThresholds
{
    public double Lines { get; set; }
    public double Branches { get; set; }
    public double Functions { get; set; }
    public double Statements { get; set; }
    public bool ThresholdsMet { get; set; }
}