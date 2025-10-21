namespace PlaywrightServerMcp.Models;

/// <summary>
/// Dependency analysis
/// </summary>
public class DependencyAnalysis
{
    public AngularDependencies Angular { get; set; } = new();
    public List<ThirdPartyDependency> ThirdParty { get; set; } = [];
    public List<string> DevDependencies { get; set; } = [];
    public List<string> PeerDependencies { get; set; } = [];
    public SecurityAnalysis Security { get; set; } = new();
    public VersionAnalysis Versions { get; set; } = new();
}