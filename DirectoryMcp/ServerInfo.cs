using System.Text.Json.Serialization;

namespace DirectoryMcp;

/// <summary>
/// Information about a single MCP server API.
/// </summary>
public class ServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}