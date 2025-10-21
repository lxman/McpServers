namespace PlaywrightServerMcp.Models;

/// <summary>
/// Project structure analysis
/// </summary>
public class ProjectStructure
{
    public string ArchitecturePattern { get; set; } = string.Empty; // monorepo, single-project, micro-frontends
    public bool IsMonorepo { get; set; }
    public int ApplicationCount { get; set; }
    public int LibraryCount { get; set; }
    public List<string> SharedLibraries { get; set; } = [];
    public DependencyGraph Dependencies { get; set; } = new();
}