namespace PlaywrightServerMcp.Models;

/// <summary>
/// Project dependency details
/// </summary>
public class ProjectDependency
{
    public string Name { get; set; } = string.Empty;
    public List<string> DependsOn { get; set; } = [];
    public List<string> UsedBy { get; set; } = [];
    public int DepthLevel { get; set; }
}