namespace PlaywrightServerMcp.Models;

/// <summary>
/// Best practices validation
/// </summary>
public class BestPracticesValidation
{
    public int Score { get; set; } // 0-100
    public List<string> Violations { get; set; } = [];
    public List<string> Improvements { get; set; } = [];
    public PerformanceChecks Performance { get; set; } = new();
    public MaintenanceChecks Maintenance { get; set; } = new();
}