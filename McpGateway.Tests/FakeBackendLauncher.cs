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

    /// <summary>
    /// Makes starts unhealthy from this start number onward, counting every Start call from 1.
    /// Unlike <see cref="Unhealthy"/>, which fails every start including the first, this lets a
    /// test bring earlier backends up healthy and fail a later one -- the case where an activation
    /// has to undo replacements it already started.
    /// </summary>
    public int UnhealthyFromStartNumber { get; set; } = int.MaxValue;

    /// <summary>
    /// Delays the port file so a test can act while a start is genuinely in flight. It must delay
    /// the port file rather than Start itself: Start runs synchronously inside Lazy.Value on the
    /// caller's thread, so a sleep here would be spent before GetOrStartAsync ever returns a Task,
    /// leaving nothing in flight to cancel into.
    /// </summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.Zero;

    public int StartCount { get; private set; }

    private readonly List<IBackendHandle> _handles = [];

    /// <summary>
    /// Every handle Start has returned, in call order (index 0 is the first call). Lets a test
    /// confirm a specific start was later torn down -- e.g. a replacement that came up healthy but
    /// was then stopped because a sibling start in the same activation failed -- without the
    /// production code needing to expose it anywhere.
    /// </summary>
    public IReadOnlyList<IBackendHandle> Handles => _handles;

    public IBackendHandle Start(BackendLaunchRequest request)
    {
        StartCount++;

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        WebApplication app = builder.Build();

        bool unhealthy = Unhealthy || StartCount >= UnhealthyFromStartNumber;
        app.MapGet("/health", () => unhealthy
            ? Results.StatusCode(500)
            : Results.Json(new { status = "ok", version = request.Version }));

        app.MapPost("/mcp", (HttpContext ctx) => Results.Json(new
        {
            version = request.Version,
            clientHeader = ctx.Request.Headers["X-Mcp-Client"].FirstOrDefault(),
            authHeader = ctx.Request.Headers.Authorization.FirstOrDefault(),
            query = ctx.Request.QueryString.Value
        }));
        app.MapPost("/admin/shutdown", () => Results.Accepted());

        app.Start();

        int port = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

        int pid = _nextPid++;

        if (!SuppressPortFile)
        {
            if (StartDelay > TimeSpan.Zero)
            {
                // Write it late and asynchronously, so the supervisor sits in WaitForPortFileAsync
                // for the duration -- a real in-flight window a test can cancel or stop into.
                TimeSpan delay = StartDelay;
                string path = request.PortFilePath;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay);
                    await PortFile.WriteAsync(path, port, pid);
                });
            }
            else
            {
                PortFile.WriteAsync(request.PortFilePath, port, pid).GetAwaiter().GetResult();
            }
        }

        var handle = new FakeHandle(app, pid);
        _handles.Add(handle);
        return handle;
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
