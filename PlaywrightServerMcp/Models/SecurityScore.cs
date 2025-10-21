namespace PlaywrightServerMcp.Models;

/// <summary>
/// Security scoring
/// </summary>
public class SecurityScore
{
    public int Overall { get; set; } // 0-100
    public int DependencyHealth { get; set; }
    public int UpdateCompliance { get; set; }
    public string Risk { get; set; } = string.Empty; // low, medium, high, critical
}