namespace DebugMcp.Models;

/// <summary>
/// Helper class for variable information.
/// </summary>
public class VariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = "unknown";
}