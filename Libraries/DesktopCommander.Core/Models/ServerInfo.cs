namespace DesktopCommander.Core.Models;

/// <summary>
/// Information about an MCP server
/// </summary>
public class ServerInfo
{
    public required string Name { get; set; }
    public required string Url { get; set; }
    public int? Port { get; set; }
    public string? ProjectPath { get; set; }
    public string? StartCommand { get; set; }
    public List<string>? InitCommands { get; set; }
    public bool RequiresInit { get; set; } = false;
    public string? InitCheckPath { get; set; }
}