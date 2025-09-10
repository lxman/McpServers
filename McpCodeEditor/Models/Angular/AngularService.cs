namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Angular service injection details
/// </summary>
public class AngularService
{
    public string ServiceName { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty; // How it's named in component
    public string ImportPath { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public List<string> UsedMethods { get; set; } = []; // Methods called on this service
    public int InjectionLine { get; set; }
}
