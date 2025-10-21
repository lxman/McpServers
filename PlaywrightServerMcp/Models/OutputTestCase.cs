namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual output test case
/// </summary>
public class OutputTestCase
{
    public string TestName { get; set; } = string.Empty;
    public string TriggerAction { get; set; } = string.Empty;
    public bool EventEmitted { get; set; }
    public object? EventPayload { get; set; }
    public bool PayloadValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
}