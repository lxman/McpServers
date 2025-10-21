namespace PlaywrightServerMcp.Models;

/// <summary>
/// Implementation phase details
/// </summary>
public class ImplementationPhase
{
    public int PhaseNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RecommendationIds { get; set; } = [];
    public string EstimatedDuration { get; set; } = string.Empty;
    public List<string> Deliverables { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
}