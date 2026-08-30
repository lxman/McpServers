using McpGateway.Configuration;
using McpGateway.Routing;
using McpGateway.Security;
using McpGateway.Supervision;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;
using Yarp.ReverseProxy.Forwarder;

namespace McpGateway;

public sealed record GatewayBuildOptions
{
    public required string ManifestPath { get; init; }
    public required string TokenPath { get; init; }
    public required string RepoRoot { get; init; }
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

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(ManifestStore.Load(options.ManifestPath));
        builder.Services.AddSingleton<IBackendLauncher, ProcessBackendLauncher>();
        builder.Services.AddSingleton(new HealthProbe(new HttpClient()));
        builder.Services.AddSingleton(sp => new BackendSupervisor(
            sp.GetRequiredService<ManifestStore>(),
            sp.GetRequiredService<IBackendLauncher>(),
            sp.GetRequiredService<HealthProbe>(),
            options,
            token,
            sp.GetRequiredService<ILogger<BackendSupervisor>>()));

        builder.Services.AddHttpForwarder();
        builder.Services.AddSingleton<McpForwarder>();

        configureServices?.Invoke(builder.Services);

        WebApplication app = builder.Build();

        app.UseMiddleware<BearerAuthMiddleware>(token);

        app.MapGet("/admin/servers", (ManifestStore manifest) => Results.Json(
            manifest.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    pair.Value.Pool,
                    pair.Value.ActiveVersion,
                    pair.Value.OverlapAllowed,
                    pair.Value.EagerStart
                })));

        app.MapPost("/{server}/mcp", (HttpContext ctx, McpForwarder fwd, string server) =>
            fwd.ForwardAsync(ctx, server, "/mcp"));

        app.MapGet("/{server}/health", (HttpContext ctx, McpForwarder fwd, string server) =>
            fwd.ForwardAsync(ctx, server, "/health"));

        return app;
    }
}
