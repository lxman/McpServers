namespace McpCodeEditor.Models.Analysis;

/// <summary>
/// Represents a reference between projects (ProjectReference or PackageReference)
/// </summary>
public class ProjectReference
{
    /// <summary>
    /// Type of reference (Project, Package, Assembly)
    /// </summary>
    public ProjectReferenceType Type { get; set; }

    /// <summary>
    /// Source project path that contains the reference
    /// </summary>
    public string SourceProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Target path or identifier being referenced
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// Original reference text from project file
    /// </summary>
    public string OriginalReference { get; set; } = string.Empty;

    /// <summary>
    /// Version specification (for package references)
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Whether this is a development-only dependency
    /// </summary>
    public bool IsDevDependency { get; set; }

    /// <summary>
    /// Confidence level of this reference detection (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; } = 1.0;

    /// <summary>
    /// Additional metadata about the reference
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    public override string ToString()
    {
        return $"{Type}: {SourceProjectPath} -> {TargetPath}";
    }
}

/// <summary>
/// Types of project references
/// </summary>
public enum ProjectReferenceType
{
    /// <summary>
    /// Unknown or unclassified reference
    /// </summary>
    Unknown,

    /// <summary>
    /// ProjectReference to another project in solution
    /// </summary>
    Project,

    /// <summary>
    /// PackageReference to NuGet package
    /// </summary>
    Package,

    /// <summary>
    /// Reference to system/framework assembly
    /// </summary>
    Framework,

    /// <summary>
    /// Direct assembly reference
    /// </summary>
    Assembly
}
