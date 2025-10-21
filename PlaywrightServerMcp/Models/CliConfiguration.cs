namespace PlaywrightServerMcp.Models;

/// <summary>
/// CLI configuration
/// </summary>
public class CliConfiguration
{
    public Dictionary<string, object> Warnings { get; set; } = new();
    public Dictionary<string, object> Analytics { get; set; } = new();
    public Dictionary<string, object> Cache { get; set; } = new();
    public Dictionary<string, object> DefaultCollection { get; set; } = new();
}