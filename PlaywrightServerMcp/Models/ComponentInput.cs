namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component input property definition
/// </summary>
public class ComponentInput
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool HasDefaultValue { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public List<string> AllowedValues { get; set; } = [];
    public string ValidationRules { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HasTransform { get; set; }
    public string TransformFunction { get; set; } = string.Empty;
}