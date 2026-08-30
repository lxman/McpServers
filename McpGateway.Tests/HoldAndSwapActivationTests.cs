using McpGateway;
using McpGateway.Configuration;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class HoldAndSwapActivationTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-holdswap-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly ManifestStore _manifest;
    private readonly BackendSupervisor _supervisor;
    private readonly ActivationService _activation;

    public HoldAndSwapActivationTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "exclusive": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "shared",
            "overlapAllowed": false, "startupTimeoutSeconds": 10
          }
        }
        """);

        _manifest = ManifestStore.Load(manifestPath);
        _supervisor = new BackendSupervisor(
            _manifest, _launcher, new HealthProbe(new HttpClient()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                RepoRoot = _root
            },
            "shutdown-token", NullLogger<BackendSupervisor>.Instance);

        _activation = new ActivationService(
            _supervisor, _manifest, NullLogger<ActivationService>.Instance);
    }

    [Fact]
    public async Task Activate_NeverRunsTwoInstancesAtOnce()
    {
        var key = new BackendKey("exclusive", "");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        _launcher.ObserveConcurrency = true;

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(before.Handle.HasExited);
        Assert.Equal(1, _launcher.MaxConcurrentLive);
    }

    [Fact]
    public async Task Activate_HoldsArrivingRequestsRatherThanRefusingThem()
    {
        var key = new BackendKey("exclusive", "");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        _launcher.StartDelay = TimeSpan.FromMilliseconds(400);

        Task<ActivationResult> activating = _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        await Task.Delay(150, TestContext.Current.CancellationToken);

        // Arrives mid-swap. Must wait for the new backend, not fail.
        BackendInstance served = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.True((await activating).Succeeded);
        Assert.Equal("v-two", served.Version);
    }

    [Fact]
    public async Task Activate_RestartsThePreviousVersion_WhenTheNewOneFailsToStart()
    {
        var key = new BackendKey("exclusive", "");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        _launcher.Unhealthy = true;

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);

        // No old process to fall back to, so the gateway brings v-one back up.
        _launcher.Unhealthy = false;
        BackendInstance recovered = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.Equal("v-one", recovered.Version);
        Assert.True(_manifest.TryGet("exclusive", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_ReturnsAFailedResult_RatherThanThrowing_WhenStoppingTheOldBackendFails()
    {
        var key = new BackendKey("exclusive", "");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        // Simulates a real process kill failing -- BackendSupervisor.StopAsync has already removed
        // the pool entry by the time this throws, so the old process's fate is unknown. The point
        // of this test is that ActivateAsync must not let that propagate as an unhandled exception.
        _launcher.ThrowOnStop = true;

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.BackendsSwapped);
        Assert.Contains("may still be alive", result.Error, StringComparison.OrdinalIgnoreCase);

        // Nothing was swapped, so the manifest correctly still says the old version -- whatever the
        // old process's true state, this at least is not a fleet/manifest disagreement.
        Assert.True(_manifest.TryGet("exclusive", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);

        _launcher.ThrowOnStop = false;
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
