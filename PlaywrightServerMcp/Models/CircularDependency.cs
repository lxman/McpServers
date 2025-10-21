namespace PlaywrightServerMcp.Models;

/// <summary>
/// Circular dependency information
/// </summary>
public class CircularDependency
{
    public List<string> Cycle { get; set; } = [];
    public string Severity { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
}