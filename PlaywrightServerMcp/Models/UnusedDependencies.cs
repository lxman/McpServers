namespace PlaywrightServerMcp.Models;

/// <summary>
/// Unused dependencies analysis
/// </summary>
public class UnusedDependencies
{
    public List<string> CompletelyUnused { get; set; } = [];
    public List<string> PartiallyUnused { get; set; } = [];
    public long PotentialSavings { get; set; }
    public List<string> SafeToRemove { get; set; } = [];
    public List<string> RequiresInvestigation { get; set; } = [];
}