namespace PlaywrightServerMcp.Models;

/// <summary>
/// Technology stack recommendations
/// </summary>
public class TechStackRecommendations
{
    public List<string> Upgrades { get; set; } = [];
    public List<string> Alternatives { get; set; } = [];
    public List<string> NewTechnologies { get; set; } = [];
}