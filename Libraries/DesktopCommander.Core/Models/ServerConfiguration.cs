namespace DesktopCommander.Core.Models;

/// <summary>
/// Root configuration loaded from servers.json
/// </summary>
public class ServerConfiguration
{
    public Dictionary<string, ServerInfo> Servers { get; set; } = new();
    public string? Usage { get; set; }
}