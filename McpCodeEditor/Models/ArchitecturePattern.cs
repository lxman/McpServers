namespace McpCodeEditor.Models;

/// <summary>
/// Represents different architecture patterns that can be detected
/// </summary>
public enum ArchitectureType
{
    Unknown,
    AngularDotNetApi,
    ReactNodeJsDatabase,
    McpServerClient,
    WpfDotNetSharedLibs,
    MicroservicesSharedInfra,
    MonoRepoMultiProject,
    FrontendBackendSeparated
}

/// <summary>
/// Detailed information about a detected architecture pattern
/// </summary>
public class ArchitecturePattern
{
    public ArchitectureType Type { get; set; } = ArchitectureType.Unknown;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } = 0.0; // 0.0 to 1.0
    public List<string> DetectionReasons { get; set; } = [];
    public List<string> ProjectPaths { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// The root path where this pattern was detected
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Detected project relationships within this pattern
    /// </summary>
    public List<ProjectRelationship> Relationships { get; set; } = [];
    
    /// <summary>
    /// Technologies and frameworks detected in this pattern
    /// </summary>
    public List<string> Technologies { get; set; } = [];
    
    /// <summary>
    /// Indicators that led to pattern detection
    /// </summary>
    public List<PatternIndicator> Indicators { get; set; } = [];
}

/// <summary>
/// Specific indicator that contributes to pattern detection
/// </summary>
public class PatternIndicator
{
    public string Type { get; set; } = string.Empty; // e.g., "file", "dependency", "naming"
    public string Value { get; set; } = string.Empty; // e.g., "angular.json", "Microsoft.AspNetCore"
    public string Location { get; set; } = string.Empty; // file path or location where found
    public double Weight { get; set; } = 1.0; // contribution to confidence score
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Template for defining how to detect specific architecture patterns
/// </summary>
public class ArchitecturePatternTemplate
{
    public ArchitectureType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// File patterns that indicate this architecture
    /// </summary>
    public List<string> FileIndicators { get; set; } = [];
    
    /// <summary>
    /// Directory patterns that indicate this architecture
    /// </summary>
    public List<string> DirectoryIndicators { get; set; } = [];
    
    /// <summary>
    /// Dependency patterns (package names, assemblies) that indicate this architecture
    /// </summary>
    public List<string> DependencyIndicators { get; set; } = [];
    
    /// <summary>
    /// Naming patterns that indicate this architecture
    /// </summary>
    public List<string> NamingPatterns { get; set; } = [];
    
    /// <summary>
    /// Minimum confidence threshold for this pattern to be considered valid
    /// </summary>
    public double MinConfidenceThreshold { get; set; } = 0.6;
    
    /// <summary>
    /// Required technologies for this pattern
    /// </summary>
    public List<string> RequiredTechnologies { get; set; } = [];
    
    /// <summary>
    /// Optional technologies that boost confidence if found
    /// </summary>
    public List<string> OptionalTechnologies { get; set; } = [];
}
