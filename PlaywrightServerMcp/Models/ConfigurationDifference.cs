namespace PlaywrightServerMcp.Models;

/// <summary>
/// Configuration difference details
/// </summary>
public class ConfigurationDifference
{
    public string Property { get; set; } = string.Empty;
    public object ProductionValue { get; set; } = new();
    public object DevelopmentValue { get; set; } = new();
    public string Impact { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}