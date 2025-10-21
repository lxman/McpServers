namespace PlaywrightServerMcp.Models;

/// <summary>
/// Loading performance metrics
/// </summary>
public class LoadingMetrics
{
    public double EstimatedDownloadTime3G { get; set; }
    public double EstimatedDownloadTime4G { get; set; }
    public double EstimatedDownloadTimeFiber { get; set; }
    public double EstimatedParseTime { get; set; }
    public double EstimatedExecutionTime { get; set; }
    public double TotalLoadTime { get; set; }
}