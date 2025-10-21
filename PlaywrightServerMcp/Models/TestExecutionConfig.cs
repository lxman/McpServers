namespace PlaywrightServerMcp.Models;

/// <summary>
/// Configuration for test execution
/// </summary>
public class TestExecutionConfig
{
    public string WorkingDirectory { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300; // 5 minutes default
    public bool WatchMode { get; set; }
    public bool HeadlessMode { get; set; } = true;
    public bool CiMode { get; set; }
    public bool CodeCoverage { get; set; } = true;
    public string Browser { get; set; } = "chrome"; // chrome, firefox, edge
    public string TestPattern { get; set; } = string.Empty; // specific test files/patterns
    public string ConfigFile { get; set; } = string.Empty; // custom config file
    public bool GenerateReports { get; set; } = true;
    public string ReportFormat { get; set; } = "json"; // json, junit, lcov
    public bool ValidateAngularProject { get; set; } = true;
}