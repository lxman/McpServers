using System.Text;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Http;

namespace McpGateway.Security;

/// <summary>
/// Guards every gateway route with the client-facing bearer token. Backends guard their own ports
/// separately, with a different token -- see <see cref="BackendToken"/>.
/// </summary>
public sealed class BearerAuthMiddleware(RequestDelegate next, string expectedToken)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(expectedToken);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!BearerToken.Matches(_expected, context))
        {
            await BearerToken.ChallengeAsync(context);
            return;
        }

        await next(context);
    }
}
