namespace PlaywrightServerMcp.Models;

/// <summary>
/// Security analysis for dependencies
/// </summary>
public class SecurityAnalysis
{
    public int VulnerabilityCount { get; set; }
    public List<SecurityVulnerability> Vulnerabilities { get; set; } = [];
    public List<string> OutdatedPackages { get; set; } = [];
    public SecurityScore Score { get; set; } = new();
}