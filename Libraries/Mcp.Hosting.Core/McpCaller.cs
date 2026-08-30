using Microsoft.AspNetCore.Http;

namespace Mcp.Hosting.Core;

/// <summary>
/// The calling client's identity, as supplied by the gateway. Stage 3 servers ignore this — the
/// gateway keeps them isolated by running one backend per client. Servers that later move to a
/// shared pool read it to scope their own state per caller.
/// </summary>
public static class McpCaller
{
    public const string HeaderName = "X-Mcp-Client";
    public const string Unknown = "default";

    private static IHttpContextAccessor? _accessor;

    internal static void Configure(IHttpContextAccessor accessor) => _accessor = accessor;

    public static string ClientId
    {
        get
        {
            HttpContext? context = _accessor?.HttpContext;
            if (context is null) return Unknown;

            string? value = context.Request.Headers[HeaderName].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value) ? Unknown : value;
        }
    }
}
