namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Angular lifecycle hook implementation
/// </summary>
public class AngularLifecycleHook
{
    public string Name { get; set; } = string.Empty; // ngOnInit, ngOnDestroy, etc.
    public string Interface { get; set; } = string.Empty; // OnInit, OnDestroy, etc.
    public int Line { get; set; }
    public bool IsImplemented { get; set; }
    public bool IsAsync { get; set; }
}
