namespace PlaywrightServerMcp.Models;

/// <summary>
/// Validation warning details
/// </summary>
public class ValidationWarning
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}