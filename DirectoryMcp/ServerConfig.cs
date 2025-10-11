using System.Text.Json.Serialization;

namespace DirectoryMcp;

/// <summary>
/// Configuration model for server registry.
/// </summary>
public class ServerConfig
{
    [JsonPropertyName("servers")]
    public Dictionary<string, ServerInfo> Servers { get; set; } = new();

    [JsonPropertyName("usage")]
    public string Usage { get; set; } = string.Empty;
}
