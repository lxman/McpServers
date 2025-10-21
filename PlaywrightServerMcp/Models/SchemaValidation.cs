namespace PlaywrightServerMcp.Models;

/// <summary>
/// Schema validation results
/// </summary>
public class SchemaValidation
{
    public bool SchemaValid { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public List<string> SchemaViolations { get; set; } = [];
    public bool UpgradeRecommended { get; set; }
}