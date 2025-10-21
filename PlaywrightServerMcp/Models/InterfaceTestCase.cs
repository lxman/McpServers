namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual interface test case
/// </summary>
public class InterfaceTestCase
{
    public string TestName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public List<object?> Parameters { get; set; } = [];
    public object? ExpectedResult { get; set; }
    public object? ActualResult { get; set; }
    public bool Passed { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
}