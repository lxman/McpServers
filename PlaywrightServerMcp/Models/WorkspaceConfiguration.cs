namespace PlaywrightServerMcp.Models;

/// <summary>
/// Angular workspace configuration structure
/// </summary>
public class WorkspaceConfiguration
{
    public int Version { get; set; }
    public string DefaultProject { get; set; } = string.Empty;
    public int ProjectCount { get; set; }
    public List<string> ProjectNames { get; set; } = [];
    public SchemaInformation Schema { get; set; } = new();
    public GlobalSettings GlobalSettings { get; set; } = new();
    public CliConfiguration Cli { get; set; } = new();
}