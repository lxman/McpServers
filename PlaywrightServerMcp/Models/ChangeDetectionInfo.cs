namespace PlaywrightServerMcp.Models;

/// <summary>
/// Change detection strategy information
/// </summary>
public class ChangeDetectionInfo
{
    public string Strategy { get; set; } = string.Empty; // Default, OnPush
    public bool UsesSignals { get; set; }
    public bool UsesObservables { get; set; }
    public bool HasImmutableInputs { get; set; }
    public List<string> InputDependencies { get; set; } = [];
}