namespace PlaywrightServerMcp.Models;

/// <summary>
/// Interface validation test result
/// </summary>
public class InterfaceValidationResult
{
    public string InterfaceName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<InterfaceTestCase> TestCases { get; set; } = [];
    public string ErrorMessage { get; set; } = string.Empty;
    public ValidationMetrics Metrics { get; set; } = new();
}