namespace PlaywrightServerMcp.Models;

/// <summary>
/// Technology stack analysis
/// </summary>
public class TechnologyStack
{
    public string AngularVersion { get; set; } = string.Empty;
    public string TypeScriptVersion { get; set; } = string.Empty;
    public string NodeVersion { get; set; } = string.Empty;
    public List<string> UILibraries { get; set; } = [];
    public List<string> StateManagement { get; set; } = [];
    public List<string> TestingFrameworks { get; set; } = [];
    public string BuildTool { get; set; } = string.Empty;
    public TechStackRecommendations Recommendations { get; set; } = new();
}