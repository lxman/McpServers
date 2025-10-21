namespace PlaywrightServerMcp.Models;

/// <summary>
/// Validation error details
/// </summary>
public class ValidationError
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Fix { get; set; } = string.Empty;
}