using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mcp.Hosting.Core;

public static class McpHostApplicationExtensions
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public static WebApplication MapMcpHost(this WebApplication app)
    {
        McpHostOptions options = app.Services.GetRequiredService<McpHostOptions>();
        McpCaller.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

        app.MapMcp("/mcp");

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            name = options.ServerName,
            version = options.Version,
            pid = Environment.ProcessId,
            uptimeSeconds = (DateTimeOffset.UtcNow - StartedAt).TotalSeconds
        }));

        app.MapPost("/admin/shutdown", (HttpContext ctx, IHostApplicationLifetime lifetime) =>
        {
            if (!TokenMatches(options.ShutdownToken, ctx)) return Results.Unauthorized();

            lifetime.StopApplication();
            return Results.Accepted();
        });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            if (options.PortFilePath is null) return;

            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Mcp.Hosting.Core");

            try
            {
                int port = ResolveBoundPort(app);

                PortFile.WriteAsync(options.PortFilePath, port, Environment.ProcessId)
                    .GetAwaiter().GetResult();

                logger.LogInformation(
                    "{Server} listening on 127.0.0.1:{Port}, port file at {Path}",
                    options.ServerName, port, options.PortFilePath);
            }
            catch (Exception ex)
            {
                // Without a port file the gateway can never route to us, so failing loudly here is
                // better than idling as an unreachable process.
                logger.LogCritical(ex, "Could not write port file at {Path}", options.PortFilePath);
                throw;
            }
        });

        return app;
    }

    private static bool TokenMatches(string? expected, HttpContext ctx)
    {
        if (string.IsNullOrEmpty(expected)) return false;

        string? presented = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (presented is null || !presented.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(presented["Bearer ".Length..]));
    }

    private static int ResolveBoundPort(WebApplication app)
    {
        IServerAddressesFeature? feature = app.Services
            .GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();

        string? address = feature?.Addresses.FirstOrDefault();
        if (address is null)
        {
            throw new InvalidOperationException("Kestrel reported no bound address.");
        }

        return new Uri(address).Port;
    }
}
