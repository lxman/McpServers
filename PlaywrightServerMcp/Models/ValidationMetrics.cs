namespace PlaywrightServerMcp.Models;

/// <summary>
/// Validation metrics for test results
/// </summary>
public class ValidationMetrics
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageTestTime => TotalTests > 0 ? TimeSpan.FromMilliseconds(TotalExecutionTime.TotalMilliseconds / TotalTests) : TimeSpan.Zero;
}