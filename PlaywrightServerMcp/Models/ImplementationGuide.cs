namespace PlaywrightServerMcp.Models;

/// <summary>
/// Implementation guide for optimization recommendations
/// </summary>
public class ImplementationGuide
{
    public List<ImplementationPhase> Phases { get; set; } = [];
    public List<string> Prerequisites { get; set; } = [];
    public List<string> Tools { get; set; } = [];
    public List<string> Resources { get; set; } = [];
    public string EstimatedTimeline { get; set; } = string.Empty;
}