namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual test suite result
/// </summary>
public class TestSuiteResult
{
    public string Name { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Tests { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public List<IndividualTestResult> TestResults { get; set; } = [];
}