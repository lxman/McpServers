namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // component, dependency, chunk, asset
    public string Priority { get; set; } = string.Empty; // high, medium, low
    public long PotentialSavings { get; set; }
    public double ImplementationEffort { get; set; } // 1-10 scale
    public string Impact { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = [];
    public List<string> AffectedFiles { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
    public string EstimatedTime { get; set; } = string.Empty;
    public Dictionary<string, object> Examples { get; set; } = new();
}