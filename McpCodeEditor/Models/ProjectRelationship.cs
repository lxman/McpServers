namespace McpCodeEditor.Models;

/// <summary>
/// Types of relationships between projects
/// </summary>
public enum RelationshipType
{
    Unknown,
    
    // Direct dependencies
    ProjectReference,
    PackageDependency,
    
    // Architectural relationships
    FrontendToBackend,
    ClientToServer,
    SharedLibrary,
    
    // Communication patterns
    ApiConsumer,
    ApiProvider,
    MessageQueue,
    Database,
    
    // Development relationships
    SameRepository,
    SameSolution,
    SameTeam,
    
    // Infrastructure relationships
    SameDeployment,
    SameContainer,
    SameCluster,
    
    // MCP specific
    McpServer,
    McpClient
}

/// <summary>
/// Represents a relationship between two projects
/// </summary>
public class ProjectRelationship
{
    public string SourceProjectPath { get; set; } = string.Empty;
    public string TargetProjectPath { get; set; } = string.Empty;
    public RelationshipType Type { get; set; } = RelationshipType.Unknown;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; } = 0.0; // 0.0 to 1.0
    
    /// <summary>
    /// Evidence that supports this relationship
    /// </summary>
    public List<RelationshipEvidence> Evidence { get; set; } = [];
    
    /// <summary>
    /// Direction of the relationship (if applicable)
    /// </summary>
    public RelationshipDirection Direction { get; set; } = RelationshipDirection.Bidirectional;
    
    /// <summary>
    /// Strength of the relationship (how tightly coupled)
    /// </summary>
    public RelationshipStrength Strength { get; set; } = RelationshipStrength.Medium;
    
    /// <summary>
    /// Additional metadata about the relationship
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// When this relationship was detected
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Direction of a project relationship
/// </summary>
public enum RelationshipDirection
{
    Bidirectional,  // Both projects depend on each other
    SourceToTarget, // Source depends on Target
    TargetToSource  // Target depends on Source
}

/// <summary>
/// Strength of coupling between projects
/// </summary>
public enum RelationshipStrength
{
    Weak,    // Loose coupling (e.g., HTTP API calls)
    Medium,  // Moderate coupling (e.g., shared interfaces)
    Strong   // Tight coupling (e.g., direct assembly references)
}

/// <summary>
/// Evidence supporting a project relationship
/// </summary>
public class RelationshipEvidence
{
    public string Type { get; set; } = string.Empty; // e.g., "file_reference", "package_dependency", "api_endpoint"
    public string Location { get; set; } = string.Empty; // where the evidence was found
    public string Details { get; set; } = string.Empty; // specific details about the evidence
    public double Weight { get; set; } = 1.0; // how much this evidence contributes to confidence
    
    /// <summary>
    /// Raw data that constitutes the evidence
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Collection of relationships for analysis and reporting
/// </summary>
public class ProjectRelationshipMap
{
    public string RootPath { get; set; } = string.Empty;
    public List<ProjectRelationship> Relationships { get; set; } = [];
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Projects that were analyzed
    /// </summary>
    public List<string> AnalyzedProjects { get; set; } = [];
    
    /// <summary>
    /// Statistics about the relationships
    /// </summary>
    public RelationshipStatistics Statistics { get; set; } = new();
    
    /// <summary>
    /// Get all relationships for a specific project
    /// </summary>
    public List<ProjectRelationship> GetRelationshipsFor(string projectPath)
    {
        return Relationships.Where(r => 
            r.SourceProjectPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase) ||
            r.TargetProjectPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
    
    /// <summary>
    /// Get projects related to the specified project
    /// </summary>
    public List<string> GetRelatedProjects(string projectPath)
    {
        var related = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (ProjectRelationship relationship in GetRelationshipsFor(projectPath))
        {
            if (!relationship.SourceProjectPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
                related.Add(relationship.SourceProjectPath);
            
            if (!relationship.TargetProjectPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
                related.Add(relationship.TargetProjectPath);
        }
        
        return related.ToList();
    }
}

/// <summary>
/// Statistics about project relationships
/// </summary>
public class RelationshipStatistics
{
    public int TotalRelationships { get; set; }
    public int TotalProjects { get; set; }
    public Dictionary<RelationshipType, int> RelationshipsByType { get; set; } = new();
    public Dictionary<RelationshipStrength, int> RelationshipsByStrength { get; set; } = new();
    public double AverageConfidence { get; set; }
    public string MostConnectedProject { get; set; } = string.Empty;
    public int MaxConnectionsPerProject { get; set; }
}
