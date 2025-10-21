namespace PlaywrightServerMcp.Models;

/// <summary>
/// Global settings from angular.json
/// </summary>
public class GlobalSettings
{
    public PackageManager PackageManager { get; set; } = new();
    public Dictionary<string, object> Schematics { get; set; } = new();
    public Dictionary<string, object> Analytics { get; set; } = new();
    public Dictionary<string, object> NewProjectRoot { get; set; } = new();
}