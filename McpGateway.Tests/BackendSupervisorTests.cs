using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class BackendSupervisorTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-supervisor-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly LiveBackendRegistry _live;
    private readonly BackendSupervisor _supervisor;

    public BackendSupervisorTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "demo": {
            "project": "Demo/Demo.csproj",
            "assembly": "Demo.dll",
            "deployRoot": "deploy/demo",
            "activeVersion": "v-one",
            "pool": "per-client",
            "startupTimeoutSeconds": 10
          }
        }
        """);

        _live = new LiveBackendRegistry(Path.Combine(_root, "live"), NullLogger.Instance);

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
            _live,
            NullLogger<BackendSupervisor>.Instance);
    }

    [Fact]
    public async Task GetOrStartAsync_RecordsTheBackendInTheLiveRegistry()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        LiveBackendRecord recorded = Assert.Single(_live.Read());

        Assert.Equal("demo", recorded.Server);
        Assert.Equal("code", recorded.PoolKey);
        Assert.Equal("v-one", recorded.Version);
        Assert.Equal(instance.Handle.ProcessId, recorded.Pid);

        // The port is written a second time, once the backend has reported it. Without that the
        // registry could name the process but not what it was serving.
        Assert.Equal(instance.Port, recorded.Port);
    }

    /// <summary>
    /// Before the health gate, not after. code-assist's startup timeout is 120 seconds; a gateway
    /// killed inside that window leaves a live process behind, and only a record written this early
    /// lets the next start find it. Asserting on the final state alone cannot tell the two apart --
    /// the record written after the gate satisfies it either way.
    /// </summary>
    [Fact]
    public async Task GetOrStartAsync_RecordsTheBackend_BeforeItIsHealthy()
    {
        _launcher.StartDelay = TimeSpan.FromSeconds(2);

        Task<BackendInstance> starting = _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        LiveBackendRecord? recorded = null;
        for (var attempt = 0; attempt < 50 && recorded is null; attempt++)
        {
            recorded = _live.Read().FirstOrDefault();
            if (recorded is null) await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.False(starting.IsCompleted, "the backend came up before the assertion could run");
        Assert.NotNull(recorded);

        // Zero because the backend has not reported a port yet. The record exists anyway: the pid
        // is the part reconciliation needs.
        Assert.Equal(0, recorded.Port);
        Assert.Equal("demo", recorded.Server);

        await starting;
    }

    [Fact]
    public async Task StopAsync_ClearsTheLiveRegistryRecord()
    {
        var key = new BackendKey("demo", "code");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        await _supervisor.StopAsync(key, TestContext.Current.CancellationToken);

        Assert.Empty(_live.Read());
    }

    /// <summary>
    /// A start that never came up leaves no process to reconcile, so leaving its record behind
    /// would point a later gateway start at a pid that now belongs to something else.
    /// </summary>
    [Fact]
    public async Task FailedStart_ClearsTheLiveRegistryRecord()
    {
        _launcher.SuppressPortFile = true;

        await Assert.ThrowsAsync<BackendStartupException>(() => _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken));

        Assert.Empty(_live.Read());
    }

    [Fact]
    public async Task GetOrStartAsync_StartsAndReturnsAHealthyBackend()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        Assert.True(instance.Port > 0);
        Assert.Equal("v-one", instance.Version);
        Assert.Equal($"http://127.0.0.1:{instance.Port}", instance.DestinationPrefix);
    }

    [Fact]
    public async Task GetOrStartAsync_ReusesTheSameBackendForTheSameKey()
    {
        var key = new BackendKey("demo", "code");

        BackendInstance first = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);
        BackendInstance second = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(1, _launcher.StartCount);
    }

    [Fact]
    public async Task GetOrStartAsync_GivesDistinctBackendsToDistinctPoolKeys()
    {
        BackendInstance code = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);
        BackendInstance desktop = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "desktop"), TestContext.Current.CancellationToken);

        Assert.NotSame(code, desktop);
        Assert.NotEqual(code.Port, desktop.Port);
        Assert.Equal(2, _launcher.StartCount);
    }

    [Fact]
    public async Task GetOrStartAsync_ConcurrentCallersShareOneStart()
    {
        var key = new BackendKey("demo", "code");

        BackendInstance[] results = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ =>
                _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken)));

        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.Equal(1, _launcher.StartCount);
    }

    [Fact]
    public async Task GetOrStartAsync_Throws_WhenThePortFileNeverArrives()
    {
        _launcher.SuppressPortFile = true;

        BackendStartupException ex = await Assert.ThrowsAsync<BackendStartupException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("demo", "code"), TestContext.Current.CancellationToken));

        Assert.Contains("port file", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetOrStartAsync_Throws_WhenTheBackendIsUnhealthy()
    {
        _launcher.Unhealthy = true;

        await Assert.ThrowsAsync<BackendStartupException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("demo", "code"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOrStartAsync_RetriesAfterAFailedStart()
    {
        _launcher.SuppressPortFile = true;
        await Assert.ThrowsAsync<BackendStartupException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("demo", "code"), TestContext.Current.CancellationToken));

        _launcher.SuppressPortFile = false;
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        Assert.True(instance.Port > 0);
    }

    [Fact]
    public async Task GetOrStartAsync_Throws_ForAServerNotInTheManifest()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("nope", "code"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BeginRequest_TracksInFlightAndDrains()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        Assert.Equal(0, instance.InFlight);

        IDisposable lease = instance.BeginRequest();
        Assert.Equal(1, instance.InFlight);

        Task<bool> drain = instance.WaitForDrainAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(drain.IsCompleted);

        lease.Dispose();

        Assert.True(await drain);
        Assert.Equal(0, instance.InFlight);
    }

    [Fact]
    public async Task WaitForDrainAsync_ReturnsFalse_WhenRequestsOutlastTheTimeout()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        using IDisposable lease = instance.BeginRequest();

        Assert.False(await instance.WaitForDrainAsync(
            TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitForDrainAsync_StaysPending_UntilEveryOverlappingRequestFinishes()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        IDisposable first = instance.BeginRequest();
        IDisposable second = instance.BeginRequest();
        Assert.Equal(2, instance.InFlight);

        first.Dispose();
        Assert.Equal(1, instance.InFlight);

        // One request is still in flight, so a short drain must time out. A drain that signals
        // here would let an upgrade kill a backend mid-request -- the exact failure the
        // zero-downtime swap exists to prevent.
        Assert.False(await instance.WaitForDrainAsync(
            TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken));

        second.Dispose();

        Assert.True(await instance.WaitForDrainAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(0, instance.InFlight);
    }

    [Fact]
    public async Task GetOrStartAsync_OneCallersCancellation_LeavesTheSharedStartIntact()
    {
        _launcher.StartDelay = TimeSpan.FromMilliseconds(400);
        var key = new BackendKey("demo", "code");

        using var impatientToken = new CancellationTokenSource();
        Task<BackendInstance> impatient = _supervisor.GetOrStartAsync(key, impatientToken.Token);
        Task<BackendInstance> patient = _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        await impatientToken.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => impatient);

        BackendInstance instance = await patient;

        // The cancelled waiter must not have evicted the entry; if it did, this starts a second
        // process and orphans the first.
        BackendInstance again = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.Same(instance, again);
        Assert.Equal(1, _launcher.StartCount);
    }

    [Fact]
    public async Task StopAsync_StopsABackendThatWasStillStarting()
    {
        _launcher.StartDelay = TimeSpan.FromMilliseconds(400);
        var key = new BackendKey("demo", "code");

        Task<BackendInstance> starting = _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        await _supervisor.StopAsync(key, TestContext.Current.CancellationToken);

        BackendInstance instance = await starting;

        // StopAsync must have awaited the in-flight start and torn it down, not abandoned it.
        Assert.True(instance.Handle.HasExited);
        Assert.False(_supervisor.TryGet(key, out _));
    }

    [Fact]
    public async Task Replace_MakesTheNewInstanceVisibleToTryGetAndAll()
    {
        var key = new BackendKey("demo", "code");
        BackendInstance original = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        BackendInstance replacement = await _supervisor.StartDetachedAsync(
            key, "v-two", TestContext.Current.CancellationToken);

        BackendInstance? displaced = _supervisor.Replace(key, replacement);

        Assert.Same(original, displaced);

        // A swapped-in backend that TryGet and All cannot see is invisible to the admin API,
        // the idle reaper, and the next activation.
        Assert.True(_supervisor.TryGet(key, out BackendInstance? current));
        Assert.Same(replacement, current);
        Assert.Contains(replacement, _supervisor.All);
        Assert.DoesNotContain(original, _supervisor.All);

        await original.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopAsync_HonoursCancellation_ButStillStopsTheBackendAfterwards()
    {
        _launcher.StartDelay = TimeSpan.FromSeconds(2);
        var key = new BackendKey("demo", "code");

        Task<BackendInstance> starting = _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        using var giveUp = new CancellationTokenSource();
        await giveUp.CancelAsync();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await _supervisor.StopAsync(key, giveUp.Token);
        watch.Stop();

        // Must not have waited out the 2s start.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1),
            $"StopAsync blocked for {watch.Elapsed} despite a cancelled token");

        BackendInstance instance = await starting;

        // The continuation must still tear it down rather than leaking the process.
        await WaitUntilAsync(() => instance.Handle.HasExited, TimeSpan.FromSeconds(5));
        Assert.True(instance.Handle.HasExited);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(25);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
