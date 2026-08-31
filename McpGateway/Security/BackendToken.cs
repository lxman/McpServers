using Mcp.Hosting.Core;

namespace McpGateway.Security;

/// <summary>
/// The bearer token the gateway presents to its backends -- deliberately NOT the token clients
/// present to the gateway.
/// <para>
/// Reusing the client-facing token would mean any client holding it could skip the gateway and
/// POST straight to a backend's loopback port, bypassing pooling, the swap hold and the non-overlap
/// guarantee. This one is minted per gateway run, lives only in memory, is never written to
/// %LOCALAPPDATA%\McpGateway\token and is never handed to a client, so clients structurally cannot.
/// </para>
/// </summary>
public sealed record BackendToken(string Value)
{
    public static BackendToken Mint() => new(BearerToken.Generate());
}
