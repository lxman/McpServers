namespace SeleniumChromeTool.Models;

public class AntiDetectionConfig
{
    public List<string> UserAgents { get; set; } = [];
    public bool RequiresCookieAccept { get; set; }
    public bool RequiresLogin { get; set; }
    public bool UsesCloudflare { get; set; }
    public List<string> ProxyList { get; set; } = [];
    public string ChromeBinaryPath { get; set; } = string.Empty;
}