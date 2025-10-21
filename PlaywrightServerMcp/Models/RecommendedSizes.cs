namespace PlaywrightServerMcp.Models;

/// <summary>
/// Recommended bundle sizes based on industry standards
/// </summary>
public class RecommendedSizes
{
    public long Initial { get; set; } // 200-300KB recommended
    public long Any { get; set; } // 2MB recommended
    public long Vendor { get; set; } // 500KB recommended
    public long Application { get; set; } // 1MB recommended
    public string Rationale { get; set; } = string.Empty;
}