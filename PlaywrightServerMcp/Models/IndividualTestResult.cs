namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual test result
/// </summary>
public class IndividualTestResult
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // passed, failed, skipped
    public TimeSpan ExecutionTime { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
}