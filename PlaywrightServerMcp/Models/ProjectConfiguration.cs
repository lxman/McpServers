namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual project configuration details
/// </summary>
public class ProjectConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty; // application, library
    public string Root { get; set; } = string.Empty;
    public string SourceRoot { get; set; } = string.Empty;
    public ArchitectConfiguration Architect { get; set; } = new();
    public string Prefix { get; set; } = string.Empty;
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}