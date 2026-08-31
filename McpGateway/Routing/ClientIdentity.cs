using McpGateway.Configuration;
using Microsoft.AspNetCore.Http;

namespace McpGateway.Routing;

public static class ClientIdentity
{
    public const string HeaderName = "X-Mcp-Client";
    public const string Default = "default";

    /// <summary>
    /// The pool key for this request. Empty means every caller shares one backend.
    /// <para>
    /// "per-session" asks the OS which process owns the connection, because the protocol carries
    /// nothing that distinguishes one session from another — the revision Claude Code negotiates
    /// dropped Mcp-Session-Id, and the only identity header on the wire is one we set ourselves.
    /// That is what reproduces the isolation stdio used to give each session for free.
    /// </para>
    /// <para>
    /// "per-client" keys on the static X-Mcp-Client header. With Claude Desktop retired it has
    /// exactly one possible value, so it is kept for the day a second client application returns,
    /// not because it isolates anything today.
    /// </para>
    /// </summary>
    public static string ResolvePoolKey(HttpContext context, ServerEntry entry)
    {
        if (entry.IsShared) return string.Empty;

        if (entry.IsPerSession)
        {
            return SessionIdentity.TryResolveKey(
                context.Connection.RemotePort, context.Connection.LocalPort) ?? Default;
        }

        string? raw = context.Request.Headers[HeaderName].FirstOrDefault();

        return string.IsNullOrWhiteSpace(raw) ? Default : raw.Trim().ToLowerInvariant();
    }
}
