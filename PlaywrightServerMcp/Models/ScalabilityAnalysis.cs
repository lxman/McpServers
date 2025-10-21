namespace PlaywrightServerMcp.Models;

/// <summary>
/// Scalability analysis
/// </summary>
public class ScalabilityAnalysis
{
    public int Score { get; set; } // 0-100
    public List<string> Strengths { get; set; } = [];
    public List<string> Concerns { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public GrowthPotential Growth { get; set; } = new();
}