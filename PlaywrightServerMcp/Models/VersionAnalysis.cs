namespace PlaywrightServerMcp.Models;

/// <summary>
/// Version analysis across dependencies
/// </summary>
public class VersionAnalysis
{
    public bool AngularVersionsConsistent { get; set; }
    public List<string> VersionMismatches { get; set; } = [];
    public List<string> MajorVersionUpdatesAvailable { get; set; } = [];
    public CompatibilityMatrix Compatibility { get; set; } = new();
}