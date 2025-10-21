namespace PlaywrightServerMcp.Models;

/// <summary>
/// Architectural insights
/// </summary>
public class ArchitecturalInsights
{
    public ProjectStructure Structure { get; set; } = new();
    public ModuleArchitecture Modules { get; set; } = new();
    public ScalabilityAnalysis Scalability { get; set; } = new();
    public TechnologyStack TechStack { get; set; } = new();
}