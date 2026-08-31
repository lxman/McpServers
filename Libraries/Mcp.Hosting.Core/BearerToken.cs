using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Mcp.Hosting.Core;

/// <summary>
/// One implementation of the bearer check, shared by the gateway's client-facing middleware and by
/// every backend's own guard. Two copies of a constant-time comparison are two chances to drift.
/// </summary>
public static class BearerToken
{
    /// <summary>32 random bytes, base64url. Long enough that guessing is not a threat model.</summary>
    public static string Generate() => Convert
        .ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    /// <summary>
    /// Constant time, because a loopback listener answers fast enough for a timing oracle on a
    /// byte-by-byte comparison to be practical from another process on the same machine.
    /// </summary>
    public static bool Matches(byte[] expected, HttpContext context)
    {
        string? presented = context.Request.Headers.Authorization.FirstOrDefault();
        if (presented is null || !presented.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            expected, Encoding.UTF8.GetBytes(presented["Bearer ".Length..]));
    }

    public static Task ChallengeAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return context.Response.WriteAsync("Missing or invalid bearer token.");
    }
}
