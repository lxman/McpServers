namespace PlaywrightServerMcp.Models;

/// <summary>
/// Configuration recommendation
/// </summary>
public class ConfigurationRecommendation
{
    public string Type { get; set; } = string.Empty; // performance, security, maintainability, scalability
    public string Priority { get; set; } = string.Empty; // low, medium, high, critical
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Implementation { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = [];
    public Dictionary<string, object> Examples { get; set; } = new();
}