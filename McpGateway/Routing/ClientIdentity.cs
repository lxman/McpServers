using McpGateway.Configuration;
using Microsoft.AspNetCore.Http;

namespace McpGateway.Routing;

public static class ClientIdentity
{
    public const string HeaderName = "X-Mcp-Client";
    public const string Default = "default";

    /// <summary>
    /// The pool key for this request. Empty means every caller shares one backend. Otherwise the
    /// calling client gets its own — which is what reproduces the isolation stdio used to give
    /// each session for free.
    /// </summary>
    public static string ResolvePoolKey(HttpContext context, ServerEntry entry)
    {
        if (entry.IsShared) return string.Empty;

        string? raw = context.Request.Headers[HeaderName].FirstOrDefault();

        return string.IsNullOrWhiteSpace(raw) ? Default : raw.Trim().ToLowerInvariant();
    }
}
