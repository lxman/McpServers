namespace PlaywrightServerMcp.Models;

/// <summary>
/// Build configurations analysis
/// </summary>
public class BuildConfigurations
{
    public List<string> AvailableConfigurations { get; set; } = [];
    public string DefaultConfiguration { get; set; } = string.Empty;
    public bool HasProduction { get; set; }
    public bool HasDevelopment { get; set; }
    public bool HasTesting { get; set; }
    public ConfigurationComparison Comparison { get; set; } = new();
    public OptimizationAnalysis Optimization { get; set; } = new();
}