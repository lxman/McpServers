namespace Mcp.Hosting.Core;

public sealed class McpHostOptions
{
    public required string ServerName { get; init; }
    public string? PortFilePath { get; init; }
    public string? ShutdownToken { get; init; }
    public string Version { get; init; } = "unknown";
}
