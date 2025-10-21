namespace PlaywrightServerMcp.Models;

/// <summary>
/// Maintenance-related checks
/// </summary>
public class MaintenanceChecks
{
    public bool TestingConfigured { get; set; }
    public bool LintingConfigured { get; set; }
    public bool TypeCheckingStrict { get; set; }
    public bool DependenciesUpToDate { get; set; }
    public List<string> MaintenanceIssues { get; set; } = [];
}