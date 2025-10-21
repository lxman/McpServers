namespace PlaywrightServerMcp.Models;

/// <summary>
/// Configuration validation results
/// </summary>
public class ConfigurationValidation
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = [];
    public List<ValidationWarning> Warnings { get; set; } = [];
    public SchemaValidation Schema { get; set; } = new();
    public BestPracticesValidation BestPractices { get; set; } = new();
}