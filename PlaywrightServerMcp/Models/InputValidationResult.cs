namespace PlaywrightServerMcp.Models;

/// <summary>
/// Input validation test result
/// </summary>
public class InputValidationResult
{
    public string InputName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<InputTestCase> TestCases { get; set; } = [];
    public string ErrorMessage { get; set; } = string.Empty;
    public ValidationMetrics Metrics { get; set; } = new();
}