namespace PlaywrightServerMcp.Models;

/// <summary>
/// Output validation test result
/// </summary>
public class OutputValidationResult
{
    public string OutputName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<OutputTestCase> TestCases { get; set; } = [];
    public string ErrorMessage { get; set; } = string.Empty;
    public ValidationMetrics Metrics { get; set; } = new();
}