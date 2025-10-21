namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component lifecycle hook information
/// </summary>
public class ComponentLifecycleInfo
{
    public List<string> ImplementedHooks { get; set; } = [];
    public bool HasOnInit { get; set; }
    public bool HasOnDestroy { get; set; }
    public bool HasOnChanges { get; set; }
    public bool ProperCleanup { get; set; }
}