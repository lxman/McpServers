namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual input test case
/// </summary>
public class InputTestCase
{
    public string TestName { get; set; } = string.Empty;
    public object? InputValue { get; set; }
    public bool Passed { get; set; }
    public string ExpectedBehavior { get; set; } = string.Empty;
    public string ActualBehavior { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
}