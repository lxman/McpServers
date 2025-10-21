namespace PlaywrightServerMcp.Models;

/// <summary>
/// Result structure for Angular unit test execution
/// </summary>
public class UnitTestResult
{
    public bool Success { get; set; }
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public TestFrameworkInfo TestFramework { get; set; } = new();
    public TestMetrics Metrics { get; set; } = new();
    public List<TestSuiteResult> TestSuites { get; set; } = [];
    public List<TestFailure> Failures { get; set; } = [];
    public CoverageReport Coverage { get; set; } = new();
    public List<string> GeneratedReports { get; set; } = [];
    public TestEnvironmentInfo Environment { get; set; } = new();
}