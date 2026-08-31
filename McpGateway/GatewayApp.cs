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

    /// <summary>
    /// Runtime state the gateway writes -- active versions. Kept out of the repo so a deploy does
    /// not dirty the working tree and a git checkout cannot revert it. Required for the same
    /// reason as LiveRegistryPath: a defaulted path is one a test can write to by accident.
    /// </summary>
    public required string StatePath { get; init; }

    /// <summary>
    /// Name of the single-instance mutex. Null derives one from <see cref="LiveRegistryPath"/>,
    /// which is what it actually protects -- so tests pointed at their own registry get their own
    /// name for free, and two real gateways sharing the machine-wide registry collide as intended.
    /// </summary>
    public string? InstanceMutexName { get; init; }

    /// <summary>
    /// Where this gateway's Serilog file sink writes. Required for the same reason as
    /// LiveRegistryPath and StatePath: it was once hardcoded to the machine-wide path, so every
    /// test that built a gateway wrote into the live gateway's log -- which is the one file a real
    /// incident has to be diagnosed from.
    /// </summary>
    public required string LogPath { get; init; }

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
        StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpGateway", "state.json"),
        LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpServers", "logs", "gateway", "gateway-.log"),
        RepoRoot = repoRoot
    };

    public static WebApplication Build(
        GatewayBuildOptions options,
        Action<IServiceCollection>? configureServices = null)
    {
        // A local, deliberately not Serilog's static Log.Logger. In production one process builds
        // one gateway and the difference is invisible, but the test suite builds many in parallel:
        // a shared static means one gateway's records can land in another's sink, and that sink can
        // be disposed out from under a gateway still using it. Nothing else in the gateway reads
        // the static, so there is nothing to keep in sync.
        var logger = McpLoggingExtensions.SetupMcpLogging(options.LogPath);

        var bootstrapLoggers = new SerilogLoggerFactory(logger, dispose: false);

        // First, before the registry is read and before anything is constructed: reconciliation
        // cannot distinguish an orphan from a running gateway's live backend, so a second gateway
        // must be refused rather than allowed to kill the first one's work. Held for the process
        // lifetime -- it is registered below so DI releases it on shutdown.
        SingleInstanceGuard instanceGuard = SingleInstanceGuard.Acquire(
            options.InstanceMutexName ?? SingleInstanceGuard.NameFor(options.LiveRegistryPath),
            bootstrapLoggers.CreateLogger<SingleInstanceGuard>());

        // Before anything is served. A gateway that was killed rather than shut down leaves its
        // backends running, and Task Scheduler's RestartCount brings a fresh gateway up beside
        // them -- two instances of code-assist writing the same machine-wide index. The job object
        // in ProcessBackendLauncher normally prevents that; this catches what it cannot, including
        // orphans left by a run from before the job object existed.
        var liveBackends = new LiveBackendRegistry(
            options.LiveRegistryPath,
            bootstrapLoggers.CreateLogger<LiveBackendRegistry>());

        liveBackends.Reconcile();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(options.Url);
        builder.Logging.ClearProviders();
        // dispose: true so the app owns the file sink. On the real gateway that just means the
        // log is flushed and closed on shutdown. In tests it is what lets a class delete its own
        // temp root: the sink holds the log file open, and the root is deleted right after the app
        // is disposed.
        builder.Logging.AddSerilog(logger, dispose: true);

        string token = TokenStore.GetOrCreate(options.TokenPath);

        // A second, distinct token for the gateway -> backend hop, minted fresh each run and held
        // only in memory. See BackendToken: sharing the client-facing token would let anyone who
        // has it talk to a backend port directly and skip the gateway entirely.
        BackendToken backendToken = BackendToken.Mint();

        builder.Services.AddSingleton(_ => instanceGuard);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(backendToken);
        builder.Services.AddSingleton(liveBackends);
        builder.Services.AddSingleton(
            ManifestStore.Load(options.ManifestPath, options.StatePath));
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

        // Realized eagerly, and registered through a factory rather than as a bare instance, so
        // the container takes ownership of releasing it. An instance-registered singleton that is
        // never resolved is never disposed -- and this one has to be released when the app is
        // disposed even if the app was built and thrown away without ever being started.
        app.Services.GetRequiredService<SingleInstanceGuard>();

        app.UseMiddleware<BearerAuthMiddleware>(token);

        app.MapAdminEndpoints();

        // All three verbs the streamable-HTTP transport uses: POST for requests, GET for the
        // server-to-client SSE stream, DELETE to close a session. Claude Code negotiates
        // 2026-07-28 and is stateless, so it should only ever POST -- but Claude Desktop's
        // negotiated revision is unknown and a legacy client on the handshake path needs GET, and
        // both are about to be pointed at this. Mapping only POST 404s them at the gateway.
        app.MapMethods("/{server}/mcp", ["GET", "POST", "DELETE"],
            (HttpContext ctx, McpForwarder fwd, string server) =>
                fwd.ForwardAsync(ctx, server, "/mcp"));

        app.MapGet("/{server}/health", (HttpContext ctx, McpForwarder fwd, string server) =>
            fwd.ForwardAsync(ctx, server, "/health"));

        return app;
    }
}
