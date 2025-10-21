namespace PlaywrightServerMcp.Models;

/// <summary>
/// Architect configuration for build, test, lint, etc.
/// </summary>
public class ArchitectConfiguration
{
    public BuildTarget Build { get; set; } = new();
    public BuildTarget Serve { get; set; } = new();
    public BuildTarget Test { get; set; } = new();
    public BuildTarget Lint { get; set; } = new();
    public BuildTarget ExtractI18n { get; set; } = new();
    public List<CustomTarget> CustomTargets { get; set; } = [];
}