namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component public method definition
/// </summary>
public class ComponentMethod
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public List<MethodParameter> Parameters { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public string AccessModifier { get; set; } = string.Empty;
}