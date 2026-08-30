using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace McpGateway.Security;

public sealed class BearerAuthMiddleware(RequestDelegate next, string expectedToken)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(expectedToken);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAuthorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsync("Missing or invalid bearer token.");
            return;
        }

        await next(context);
    }

    private bool IsAuthorized(HttpContext context)
    {
        string? presented = context.Request.Headers.Authorization.FirstOrDefault();
        if (presented is null || !presented.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            _expected, Encoding.UTF8.GetBytes(presented["Bearer ".Length..]));
    }
}
