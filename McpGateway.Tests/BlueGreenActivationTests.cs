using McpGateway;
using McpGateway.Configuration;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class BlueGreenActivationTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-activate-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly ManifestStore _manifest;
    private readonly BackendSupervisor _supervisor;
    private readonly ActivationService _activation;

    public BlueGreenActivationTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "overlaps": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "per-client",
            "overlapAllowed": true, "startupTimeoutSeconds": 10
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
    public async Task Activate_SwapsARunningBackendToTheNewVersion()
    {
        var key = new BackendKey("overlaps", "code");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, result.BackendsSwapped);

        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Equal("v-two", after!.Version);
        Assert.NotSame(before, after);
        Assert.True(before.Handle.HasExited);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-two", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_SwapsEveryLiveBackendOfThatServer()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "code"), TestContext.Current.CancellationToken);
        await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "desktop"), TestContext.Current.CancellationToken);

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.Equal(2, result.BackendsSwapped);
    }

    [Fact]
    public async Task Activate_PersistsTheNewActiveVersion()
    {
        await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-two", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_WithNoLiveBackend_JustRecordsTheVersion()
    {
        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.BackendsSwapped);
        Assert.Equal(0, _launcher.StartCount);
    }

    [Fact]
    public async Task Activate_LeavesTheOldBackendServing_WhenTheNewOneIsUnhealthy()
    {
        var key = new BackendKey("overlaps", "code");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        _launcher.Unhealthy = true;

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);

        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Same(before, after);
        Assert.False(before.Handle.HasExited);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_ReportsADrainTimeout_ButStillSwaps()
    {
        var key = new BackendKey("overlaps", "code");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        // A request that never finishes is the one window where an upgrade can cost a call.
        using IDisposable stuck = before.BeginRequest();

        _activation.DrainTimeout = TimeSpan.FromMilliseconds(200);

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.DrainTimedOut);
        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Equal("v-two", after!.Version);
    }

    [Fact]
    public async Task Activate_SwapsNothing_WhenAnyBackendFailsToStart()
    {
        BackendInstance beforeCode = await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "code"), TestContext.Current.CancellationToken);
        BackendInstance beforeDesktop = await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "desktop"), TestContext.Current.CancellationToken);

        // Two backends are live, so StartCount is 2. Fail the FOURTH start: the first replacement
        // comes up healthy and the second does not, which is the only shape that distinguishes
        // all-or-nothing from swap-as-you-go. A global Unhealthy flag would fail the first
        // replacement too, and both implementations would then look identical.
        _launcher.UnhealthyFromStartNumber = 4;

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.BackendsSwapped);

        // The third Start() call (after the two initial live backends) is the replacement that
        // came up healthy before the fourth one failed. The catch block's cleanup loop is the only
        // thing that could have stopped it -- BackendSupervisor never touches a start that
        // succeeded -- so this is the cleanup loop's own signature.
        Assert.True(_launcher.Handles[2].HasExited);

        // Neither backend may have moved, and the manifest must still agree with them.
        Assert.True(_supervisor.TryGet(new BackendKey("overlaps", "code"), out BackendInstance? code));
        Assert.Same(beforeCode, code);
        Assert.False(beforeCode.Handle.HasExited);

        Assert.True(_supervisor.TryGet(new BackendKey("overlaps", "desktop"), out BackendInstance? desktop));
        Assert.Same(beforeDesktop, desktop);
        Assert.False(beforeDesktop.Handle.HasExited);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_ReturnsAFailedResult_RatherThanThrowing_WhenStoppingTheOldBackendFails()
    {
        var key = new BackendKey("overlaps", "code");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        // Simulates a real process kill failing during the drain-then-stop phase, after the new
        // backend has already been Replace()'d in. There is nothing to roll back to at that point;
        // the point of this test is that the exception must surface as a failed ActivationResult,
        // not propagate out of ActivateAsync as an unhandled exception.
        _launcher.ThrowOnStop = true;

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        _launcher.ThrowOnStop = false;

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.BackendsSwapped);
        Assert.Contains("already replaced", result.Error, StringComparison.OrdinalIgnoreCase);

        // Replace() already ran before the throw, so the new backend is live in the pool --
        // but the manifest write never ran. Fleet and manifest now genuinely disagree; the
        // failed result says so rather than hiding it behind a crash.
        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Equal("v-two", after!.Version);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
