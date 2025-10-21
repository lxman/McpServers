namespace PlaywrightServerMcp.Models;

/// <summary>
/// Test failure information
/// </summary>
public class TestFailure
{
    public string TestName { get; set; } = string.Empty;
    public string SuiteName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
}