namespace Mcp.Hosting.Core;

public sealed class McpHostOptions
{
    public required string ServerName { get; init; }
    public string? PortFilePath { get; init; }

    /// <summary>
    /// The bearer token every endpoint on this backend requires -- /mcp and /health as well as
    /// /admin/shutdown. It arrives in the MCP_SHUTDOWN_TOKEN environment variable, whose name is
    /// historical: it once guarded shutdown alone. The gateway mints it per run and it is not the
    /// token clients present to the gateway, so a client cannot use its own credential to reach a
    /// backend port directly.
    /// </summary>
    public string? AuthToken { get; init; }

    public string Version { get; init; } = "unknown";
}
