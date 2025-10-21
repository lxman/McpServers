namespace PlaywrightServerMcp.Models;

/// <summary>
/// Method parameter definition
/// </summary>
public class MethodParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
}