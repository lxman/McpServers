using McpGateway.Supervision;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McpGateway.Tests;

/// <summary>
/// Starts a real loopback Kestrel that answers /health and echoes its version, then writes the
/// port file exactly as Mcp.Hosting.Core does. Lets the supervisor be tested against the real
/// port-file-then-health-gate handshake without process spawning.
/// </summary>
public sealed class FakeBackendLauncher : IBackendLauncher
{
    private int _nextPid = 1000;

    /// <summary>Set to skip writing the port file, simulating a backend that never comes up.</summary>
    public bool SuppressPortFile { get; set; }

    /// <summary>Set to make /health return 500, simulating a backend that starts unhealthy.</summary>
    public bool Unhealthy { get; set; }

    /// <summary>Slows a start so a test can act while one is still in flight.</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.Zero;

    public int StartCount { get; private set; }

    public IBackendHandle Start(BackendLaunchRequest request)
    {
        StartCount++;

        if (StartDelay > TimeSpan.Zero) Thread.Sleep(StartDelay);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        WebApplication app = builder.Build();

        bool unhealthy = Unhealthy;
        app.MapGet("/health", () => unhealthy
            ? Results.StatusCode(500)
            : Results.Json(new { status = "ok", version = request.Version }));

        app.MapPost("/mcp", () => Results.Json(new { version = request.Version }));
        app.MapPost("/admin/shutdown", () => Results.Accepted());

        app.Start();

        int port = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

        int pid = _nextPid++;

        if (!SuppressPortFile)
        {
            PortFile.WriteAsync(request.PortFilePath, port, pid).GetAwaiter().GetResult();
        }

        return new FakeHandle(app, pid);
    }

    private sealed class FakeHandle(WebApplication app, int pid) : IBackendHandle
    {
        public int ProcessId { get; } = pid;
        public bool HasExited { get; private set; }

        public async ValueTask DisposeAsync()
        {
            if (HasExited) return;
            HasExited = true;
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
