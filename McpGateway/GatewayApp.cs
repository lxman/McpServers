using McpGateway.Configuration;
using McpGateway.Endpoints;
using McpGateway.Routing;
using McpGateway.Security;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SerilogFileWriter;
using Yarp.ReverseProxy.Forwarder;

namespace McpGateway;

public sealed record GatewayBuildOptions
{
    public required string ManifestPath { get; init; }
    public required string TokenPath { get; init; }
    public required string RepoRoot { get; init; }

    /// <summary>
    /// Directory holding one record per live backend, used to kill orphans at the next start.
    /// Deliberately required rather than defaulted: a test that forgot to point it somewhere
    /// harmless would reconcile -- and kill against -- the real machine-wide registry.
    /// </summary>
    public required string LiveRegistryPath { get; init; }

    public string Url { get; init; } = "http://127.0.0.1:7300";
}

public static class GatewayApp
{
    public static GatewayBuildOptions DefaultOptions(string repoRoot) => new()
    {
        ManifestPath = Path.Combine(repoRoot, "servers.json"),
        TokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpGateway", "token"),
        LiveRegistryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpGateway", "live"),
        RepoRoot = repoRoot
    };

    public static WebApplication Build(
        GatewayBuildOptions options,
        Action<IServiceCollection>? configureServices = null)
    {
        Log.Logger = McpLoggingExtensions.SetupMcpLogging(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpServers", "logs", "gateway", "gateway-.log"));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(options.Url);
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: false);

        string token = TokenStore.GetOrCreate(options.TokenPath);

        // A second, distinct token for the gateway -> backend hop, minted fresh each run and held
        // only in memory. See BackendToken: sharing the client-facing token would let anyone who
        // has it talk to a backend port directly and skip the gateway entirely.
        BackendToken backendToken = BackendToken.Mint();

        // Before anything is served. A gateway that was killed rather than shut down leaves its
        // backends running, and Task Scheduler's RestartCount brings a fresh gateway up beside
        // them -- two instances of code-assist writing the same machine-wide index. The job object
        // in ProcessBackendLauncher normally prevents that; this catches what it cannot, including
        // orphans left by a run from before the job object existed.
        var liveBackends = new LiveBackendRegistry(
            options.LiveRegistryPath,
            new SerilogLoggerFactory(Log.Logger, dispose: false)
                .CreateLogger<LiveBackendRegistry>());

        liveBackends.Reconcile();

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(backendToken);
        builder.Services.AddSingleton(liveBackends);
        builder.Services.AddSingleton(ManifestStore.Load(options.ManifestPath));
        builder.Services.AddSingleton<IBackendLauncher, ProcessBackendLauncher>();
        builder.Services.AddSingleton(new HealthProbe(new HttpClient(), backendToken));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(sp => new BackendSupervisor(
            sp.GetRequiredService<ManifestStore>(),
            sp.GetRequiredService<IBackendLauncher>(),
            sp.GetRequiredService<HealthProbe>(),
            options,
            backendToken.Value,
            sp.GetRequiredService<LiveBackendRegistry>(),
            sp.GetRequiredService<ILogger<BackendSupervisor>>(),
            sp.GetRequiredService<TimeProvider>()));

        builder.Services.AddHttpForwarder();
        builder.Services.AddSingleton<McpForwarder>();

        builder.Services.AddSingleton<IdleReaper>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<IdleReaper>());

        builder.Services.AddSingleton<EagerStarter>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<EagerStarter>());

        builder.Services.AddSingleton<ActivationService>();

        configureServices?.Invoke(builder.Services);

        WebApplication app = builder.Build();

        app.UseMiddleware<BearerAuthMiddleware>(token);

        app.MapAdminEndpoints();

        app.MapPost("/{server}/mcp", (HttpContext ctx, McpForwarder fwd, string server) =>
            fwd.ForwardAsync(ctx, server, "/mcp"));

        app.MapGet("/{server}/health", (HttpContext ctx, McpForwarder fwd, string server) =>
            fwd.ForwardAsync(ctx, server, "/health"));

        return app;
    }
}
