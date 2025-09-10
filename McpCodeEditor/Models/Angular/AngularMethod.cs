namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Angular component method
/// </summary>
public class AngularMethod
{
    public string Name { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public List<string> Parameters { get; set; } = [];
    public string? AccessModifier { get; set; } // public, private, protected
    public bool IsAsync { get; set; }
    public bool IsStatic { get; set; }
    public bool IsLifecycleHook { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public bool UsedInTemplate { get; set; }
    public string? Description { get; set; } // From JSDoc comments
}
