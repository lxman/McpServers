namespace PlaywrightServerMcp.Models;

/// <summary>
/// Alternative dependency suggestion
/// </summary>
public class AlternativeDependency
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long SizeSavings { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string MigrationEffort { get; set; } = string.Empty; // low, medium, high
}