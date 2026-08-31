using McpGateway;
using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace McpGateway.Tests;

public sealed class IdleReaperTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-reaper-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-08-30T12:00:00Z"));
    private readonly BackendSupervisor _supervisor;
    private readonly IdleReaper _reaper;

    public IdleReaperTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "reaps": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "per-client",
            "idleTimeoutMinutes": 30, "startupTimeoutSeconds": 10
          },
          "never-reaps": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "shared",
            "eagerStart": true,
            "idleTimeoutMinutes": 0, "startupTimeoutSeconds": 10
          },
          "shared-lazy": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "shared",
            "eagerStart": false,
            "idleTimeoutMinutes": 30, "startupTimeoutSeconds": 10
          }
        }
        """);

        _supervisor = new BackendSupervisor(
            ManifestStore.Load(manifestPath),
            _launcher,
            new HealthProbe(new HttpClient(), BackendToken.Mint()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                LiveRegistryPath = Path.Combine(_root, "live"),
                RepoRoot = _root
            },
            "backend-token",
            new LiveBackendRegistry(Path.Combine(_root, "live"), NullLogger.Instance),
            NullLogger<BackendSupervisor>.Instance, _time);

        _reaper = new IdleReaper(_supervisor, _time, NullLogger<IdleReaper>.Instance);
    }

    [Fact]
    public async Task Sweep_StopsABackendIdleLongerThanItsTimeout()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(1, await _reaper.SweepAsync(TestContext.Current.CancellationToken));
        Assert.False(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task Sweep_LeavesABackendInsideItsTimeout()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromMinutes(29));

        Assert.Equal(0, await _reaper.SweepAsync(TestContext.Current.CancellationToken));
        Assert.True(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task Sweep_NeverStopsAServerWithTimeoutZero()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("never-reaps", ""), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromDays(7));

        Assert.Equal(0, await _reaper.SweepAsync(TestContext.Current.CancellationToken));
        Assert.True(_supervisor.TryGet(new BackendKey("never-reaps", ""), out _));
    }

    [Fact]
    public async Task Sweep_LeavesABackendWithRequestsInFlight()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        using IDisposable lease = instance.BeginRequest();
        _time.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(0, await _reaper.SweepAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvictExitedAsync_DropsACrashedBackend()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        await instance.Handle.DisposeAsync();

        Assert.Equal(1, await _supervisor.EvictExitedAsync(TestContext.Current.CancellationToken));
        Assert.False(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task GetOrStartAsync_RestartsAfterACrash()
    {
        var key = new BackendKey("reaps", "code");

        BackendInstance first = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);
        await first.Handle.DisposeAsync();

        BackendInstance second = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.NotSame(first, second);
        Assert.Equal(2, _launcher.StartCount);
    }

    [Fact]
    public async Task EagerStarter_StartsOnlyTheServersMarkedEager()
    {
        var starter = new EagerStarter(
            _supervisor,
            ManifestStore.Load(Path.Combine(_root, "servers.json")),
            NullLogger<EagerStarter>.Instance);

        await starter.StartEagerServersAsync(TestContext.Current.CancellationToken);

        // "shared-lazy" is the load-bearing assertion: it is shared, so EagerStarter's IsShared
        // branch does not filter it, which leaves the EagerStart guard as the only thing keeping
        // it from starting. A per-client server would be filtered either way and prove nothing.
        Assert.True(_supervisor.TryGet(new BackendKey("never-reaps", ""), out _));
        Assert.False(_supervisor.TryGet(new BackendKey("shared-lazy", ""), out _));
        Assert.False(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task EagerStarter_DoesNotThrow_WhenAnEagerServerFailsToStart()
    {
        _launcher.SuppressPortFile = true;

        var starter = new EagerStarter(
            _supervisor,
            ManifestStore.Load(Path.Combine(_root, "servers.json")),
            NullLogger<EagerStarter>.Instance);

        // A backend that won't come up must not take the gateway down with it.
        await starter.StartEagerServersAsync(TestContext.Current.CancellationToken);

        Assert.False(_supervisor.TryGet(new BackendKey("never-reaps", ""), out _));

        // TryGet alone cannot tell "never attempted" from "attempted and failed" -- a failed start
        // is removed from the pool either way. StartCount == 1 proves "shared-lazy" (not eager)
        // was never even attempted, only "never-reaps" was.
        Assert.Equal(1, _launcher.StartCount);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
