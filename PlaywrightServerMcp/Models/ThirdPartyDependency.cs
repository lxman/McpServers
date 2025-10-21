namespace PlaywrightServerMcp.Models;

/// <summary>
/// Third-party dependency information
/// </summary>
public class ThirdPartyDependency
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // ui, state-management, testing, etc.
    public string Description { get; set; } = string.Empty;
    public bool IsDeprecated { get; set; }
    public List<string> Alternatives { get; set; } = [];
}