namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Result of Angular component analysis
/// </summary>
public class AngularComponentAnalysisResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public AngularComponent? Component { get; set; }
    public List<string> Recommendations { get; set; } = [];
    public List<string> PotentialIssues { get; set; } = [];
    public int ComplexityScore { get; set; }
    public bool NeedsRefactoring { get; set; }
}
