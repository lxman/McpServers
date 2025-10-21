namespace PlaywrightServerMcp.Models;

/// <summary>
/// Package manager configuration
/// </summary>
public class PackageManager
{
    public string Name { get; set; } = string.Empty; // npm, yarn, pnpm
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, object> Settings { get; set; } = new();
}