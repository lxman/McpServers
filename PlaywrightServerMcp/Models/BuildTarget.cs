namespace PlaywrightServerMcp.Models;

/// <summary>
/// Build target configuration
/// </summary>
public class BuildTarget
{
    public string Builder { get; set; } = string.Empty;
    public Dictionary<string, object> Options { get; set; } = new();
    public Dictionary<string, BuildConfiguration> Configurations { get; set; } = new();
    public List<string> DefaultConfiguration { get; set; } = [];
}