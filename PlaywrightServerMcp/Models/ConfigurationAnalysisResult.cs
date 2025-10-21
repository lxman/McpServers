namespace PlaywrightServerMcp.Models;

/// <summary>
/// Result structure for Angular configuration analysis
/// </summary>
public class ConfigurationAnalysisResult
{
    public bool Success { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool AngularJsonExists { get; set; }
    public bool PackageJsonExists { get; set; }
    public bool TsConfigExists { get; set; }
    public WorkspaceConfiguration WorkspaceConfig { get; set; } = new();
    public List<ProjectConfiguration> Projects { get; set; } = [];
    public BuildConfigurations BuildConfigs { get; set; } = new();
    public DependencyAnalysis Dependencies { get; set; } = new();
    public ConfigurationValidation Validation { get; set; } = new();
    public ArchitecturalInsights Insights { get; set; } = new();
    public List<ConfigurationRecommendation> Recommendations { get; set; } = [];
    public string ErrorMessage { get; set; } = string.Empty;
}