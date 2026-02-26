namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// Complete structural model of a solution: projects, references, and groupings.
/// Parsed from .slnx/.sln and .csproj files.
/// </summary>
public sealed class SolutionStructure
{
    /// <summary>
    /// Solution name (file name without extension).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Absolute path to the solution file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// All projects in the solution.
    /// </summary>
    public required List<ProjectInfo> Projects { get; init; }

    /// <summary>
    /// Solution-level folder groupings (e.g., "Libraries").
    /// </summary>
    public required List<SolutionFolderInfo> Folders { get; init; }
}

/// <summary>
/// A project within the solution, with its references and metadata.
/// </summary>
public sealed class ProjectInfo
{
    /// <summary>
    /// Project name (typically the .csproj file name without extension).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Path to the .csproj file relative to the solution root.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Solution folder this project belongs to, or null if top-level.
    /// </summary>
    public string? SolutionFolder { get; init; }

    /// <summary>
    /// Target framework (e.g., "net8.0", "net9.0").
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Output type: "Exe" for executables, "Library" for class libraries (default).
    /// </summary>
    public string? OutputType { get; init; }

    /// <summary>
    /// Names of other projects this project references.
    /// </summary>
    public required List<string> ProjectReferences { get; init; }

    /// <summary>
    /// NuGet package references with versions.
    /// </summary>
    public required List<PackageRef> PackageReferences { get; init; }

    /// <summary>
    /// Namespaces found in this project's source files (populated during indexing).
    /// </summary>
    public List<string>? Namespaces { get; set; }
}

/// <summary>
/// A NuGet package reference.
/// </summary>
public sealed class PackageRef
{
    public required string Name { get; init; }

    /// <summary>
    /// Version string, or null if managed by Directory.Packages.props (central package management).
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// A solution folder grouping.
/// </summary>
public sealed class SolutionFolderInfo
{
    /// <summary>
    /// Folder name (e.g., "Libraries").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Names of projects in this folder.
    /// </summary>
    public required List<string> ProjectNames { get; init; }
}
