namespace McpCodeEditor.Models.Analysis;

/// <summary>
/// Statistics about the dependency graph
/// </summary>
public class GraphStatistics
{
    /// <summary>
    /// Total number of projects analyzed
    /// </summary>
    public int TotalProjects { get; set; }

    /// <summary>
    /// Total number of references found
    /// </summary>
    public int TotalReferences { get; set; }

    /// <summary>
    /// Number of project-to-project references
    /// </summary>
    public int ProjectReferences { get; set; }

    /// <summary>
    /// Number of package references
    /// </summary>
    public int PackageReferences { get; set; }

    /// <summary>
    /// Number of circular dependencies detected
    /// </summary>
    public int CircularDependencies { get; set; }

    /// <summary>
    /// Maximum dependency depth
    /// </summary>
    public int MaxDependencyDepth { get; set; }

    /// <summary>
    /// Analysis confidence score (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; } = 1.0;
}
