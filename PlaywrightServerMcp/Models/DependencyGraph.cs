namespace PlaywrightServerMcp.Models;

/// <summary>
/// Dependency graph structure
/// </summary>
public class DependencyGraph
{
    public List<ProjectDependency> Projects { get; set; } = [];
    public List<CircularDependency> CircularDependencies { get; set; } = [];
    public int MaxDepth { get; set; }
    public bool HasIssues { get; set; }
}