namespace PlaywrightServerMcp.Models;

/// <summary>
/// User experience metrics
/// </summary>
public class UserExperienceMetrics
{
    public int PerformanceScore { get; set; } // 0-100
    public string UserExperienceGrade { get; set; } = string.Empty; // A, B, C, D, F
    public List<string> UserImpactFactors { get; set; } = [];
    public List<string> ImprovementAreas { get; set; } = [];
}