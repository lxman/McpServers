namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component public property definition
/// </summary>
public class ComponentProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsReadonly { get; set; }
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public string Description { get; set; } = string.Empty;
}