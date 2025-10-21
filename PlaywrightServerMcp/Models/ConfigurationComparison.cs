namespace PlaywrightServerMcp.Models;

/// <summary>
/// Configuration comparison analysis
/// </summary>
public class ConfigurationComparison
{
    public List<string> SharedOptions { get; set; } = [];
    public List<string> UniqueToProduction { get; set; } = [];
    public List<string> UniqueToDevevelopment { get; set; } = [];
    public Dictionary<string, ConfigurationDifference> Differences { get; set; } = new();
}