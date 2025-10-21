namespace PlaywrightServerMcp.Models;

/// <summary>
/// Schema information
/// </summary>
public class SchemaInformation
{
    public string Version { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsLatest { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
}