using McpCodeEditor.Services;

namespace McpCodeEditor.Models.Analysis;

/// <summary>
/// Represents a project node in the dependency graph
/// </summary>
public class ProjectNode
{
    /// <summary>
    /// Full path to the project file (.csproj, .esproj, etc.)
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Project name (filename without extension)
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Project type (from existing ProjectType enum)
    /// </summary>
    public ProjectType ProjectType { get; set; } = ProjectType.Unknown;

    /// <summary>
    /// Target framework(s) if detected
    /// </summary>
    public List<string> TargetFrameworks { get; set; } = [];

    /// <summary>
    /// Platform-specific indicators
    /// </summary>
    public List<string> PlatformIndicators { get; set; } = [];

    /// <summary>
    /// When this project was last analyzed
    /// </summary>
    public DateTime LastAnalyzed { get; set; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"{ProjectName} ({ProjectType})";
    }
}
