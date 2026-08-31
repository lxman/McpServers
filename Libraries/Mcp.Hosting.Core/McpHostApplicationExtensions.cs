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

        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Mcp.Hosting.Core");

        // Refuse to start rather than serve unauthenticated. A backend's loopback port is
        // reachable by every process on this machine, so an unguarded /mcp is unauthenticated
        // delete_index on code-assist, arbitrary command execution on desktop-commander, and the
        // plaintext SSH profiles on ssh-mcp. Coming up in that state and logging about it would be
        // worse than not coming up: the gateway would health-gate it green and route real traffic
        // to it.
        if (string.IsNullOrEmpty(options.AuthToken))
        {
            const string message =
                "No MCP_SHUTDOWN_TOKEN in the environment. Every endpoint this server exposes " +
                "requires a bearer token, and its loopback port is reachable by any process on " +
                "this machine, so it will not start without one. The gateway supplies the token; " +
                "set it by hand if you are running this server directly.";

            logger.LogCritical("{Server}: {Message}", options.ServerName, message);

            throw new InvalidOperationException($"{options.ServerName}: {message}");
        }

        McpCaller.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

        byte[] expected = Encoding.UTF8.GetBytes(options.AuthToken);

        // Every endpoint, not just /admin/shutdown: /mcp is the one that actually runs tools, and
        // /health leaks the server name, version and pid of everything the gateway supervises.
        app.Use(async (context, next) =>
        {
            if (!BearerToken.Matches(expected, context))
            {
                await BearerToken.ChallengeAsync(context);
                return;
            }

            await next(context);
        });

        app.MapMcp("/mcp");

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            name = options.ServerName,
            version = options.Version,
            pid = Environment.ProcessId,
            uptimeSeconds = (DateTimeOffset.UtcNow - StartedAt).TotalSeconds
        }));

        app.MapPost("/admin/shutdown", (IHostApplicationLifetime lifetime) =>
        {
            lifetime.StopApplication();
            return Results.Accepted();
        });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            if (options.PortFilePath is null) return;

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
