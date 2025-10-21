namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component dependency information
/// </summary>
public class ComponentDependency
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // import, service, component
    public long SizeBytes { get; set; }
    public bool IsExternal { get; set; }
    public bool IsLazyLoaded { get; set; }
    public string Source { get; set; } = string.Empty;
}