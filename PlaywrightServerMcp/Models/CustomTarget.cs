namespace PlaywrightServerMcp.Models;

/// <summary>
/// Custom architect target
/// </summary>
public class CustomTarget
{
    public string Name { get; set; } = string.Empty;
    public string Builder { get; set; } = string.Empty;
    public Dictionary<string, object> Options { get; set; } = new();
}