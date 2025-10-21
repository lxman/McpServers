namespace PlaywrightServerMcp.Models;

/// <summary>
/// Test execution metrics
/// </summary>
public class TestMetrics
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    public int TotalSuites { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
    public bool AllTestsPassed => FailedTests == 0 && TotalTests > 0;
}