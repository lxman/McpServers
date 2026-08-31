using McpGateway;
using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class HoldAndSwapActivationTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-holdswap-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Outside _root on purpose. In production the live registry lives under %LOCALAPPDATA%, not in
    /// the repo, and one test here induces a failure by making the state file unwritable -- a
    /// registry inside the same directory would keep re-creating it.
    /// </summary>
    private readonly string _liveRoot = Path.Combine(
        Path.GetTempPath(), "mcp-holdswap-live-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Its own directory, separate from _liveRoot. LiveBackendRegistry.Read globs *.json, so a
    /// state.json sitting beside the pid records would deserialize into a bogus LiveBackendRecord
    /// -- harmless while nothing here reconciles, and a trap for whoever adds a test that does.
    /// </summary>
    private readonly string _stateRoot = Path.Combine(
        Path.GetTempPath(), "mcp-holdswap-state-" + Guid.NewGuid().ToString("N"));

    private readonly string _statePath;

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
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d", "pool": "shared",
            "overlapAllowed": false, "startupTimeoutSeconds": 10
          }
        }
        """);

        _statePath = TestState.Write(_stateRoot, ("exclusive", "v-one"));
        _manifest = ManifestStore.Load(manifestPath, _statePath);
        _supervisor = new BackendSupervisor(
            _manifest, _launcher, new HealthProbe(new HttpClient(), BackendToken.Mint()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                LiveRegistryPath = _liveRoot,
                StatePath = _statePath,
                RepoRoot = _root
            },
            "backend-token",
            new LiveBackendRegistry(_liveRoot, NullLogger.Instance),
            NullLogger<BackendSupervisor>.Instance);

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
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        // UnhealthyFromStartNumber is a floor -- StartCount only increases, so "the new version's
        // start (call #2, v-two) fails but the restore's start (call #3, v-one) succeeds" cannot be
        // expressed by one static value: 2 would also catch call #3, since 3 >= 2. Lift the floor
        // from HealthStatusCaptured instead, the instant call #2's own status is locked into its
        // closure -- deterministically, not by racing a delay against the real ~10s health-check
        // timeout call #2 is about to sit in before the catch block below even runs.
        _launcher.UnhealthyFromStartNumber = 2;
        _launcher.HealthStatusCaptured += count =>
        {
            if (count == 2) _launcher.UnhealthyFromStartNumber = int.MaxValue;
        };

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "previous version was restored", result.Error, StringComparison.OrdinalIgnoreCase);

        // Assert on the instance the restore's own Replace() installed, not on a fresh
        // GetOrStartAsync -- a deleted Replace() call would leave a later GetOrStartAsync free to
        // start its own backend and satisfy a weaker assertion, hiding exactly the regression this
        // test exists to catch (see mutation check in the report).
        Assert.True(_supervisor.TryGet(key, out BackendInstance? restored));
        Assert.Equal("v-one", restored!.Version);
        Assert.NotSame(before, restored);

        Assert.True(_manifest.TryGet("exclusive", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_ReturnsAFailedResult_WhenTheOldVersionCannotBeRestoredEither()
    {
        var key = new BackendKey("exclusive", "");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        // Unlike the test above, nothing lifts the floor: both the new version's start (#2) and
        // the restore attempt (#3) land on or past it, so the restore genuinely fails too. This is
        // the "no running instance at all" outcome Finding 1 makes distinguishable from "restored."
        _launcher.UnhealthyFromStartNumber = 2;

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("CRITICAL", result.Error, StringComparison.Ordinal);
        Assert.Contains("no running instance", result.Error, StringComparison.OrdinalIgnoreCase);
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

        ActivationResult result;
        try
        {
            result = await _activation.ActivateAsync(
                "exclusive", "v-two", TestContext.Current.CancellationToken);
        }
        finally
        {
            // In a try/finally so a mutation that makes activation propagate still resets this --
            // otherwise DisposeAsync's own teardown throws too, muddying the mutation's output.
            _launcher.ThrowOnStop = false;
        }

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.BackendsSwapped);
        Assert.Contains("may still be alive", result.Error, StringComparison.OrdinalIgnoreCase);

        // Nothing was swapped, so the manifest correctly still says the old version -- whatever the
        // old process's true state, this at least is not a fleet/manifest disagreement.
        Assert.True(_manifest.TryGet("exclusive", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_ReturnsAFailedResult_WhenTheManifestWriteFails_AfterTheSwapSucceeded()
    {
        var key = new BackendKey("exclusive", "");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        // Puts a directory where the state file goes, so SetActiveVersionAsync's File.Move onto
        // it throws -- a cheap, uncontrived way to induce the "swap succeeded but the version could
        // not be recorded" sub-case without a new fake seam. Deleting a directory no longer works
        // for this: the write creates its own parent, and the state file deliberately lives outside
        // the deploy root now. The fake launcher reads nothing from disk, so starting v-two still
        // succeeds; only the state write is affected.
        File.Delete(_statePath);
        Directory.CreateDirectory(_statePath);

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.BackendsSwapped);
        Assert.Contains("manifest write failed", result.Error, StringComparison.OrdinalIgnoreCase);

        // The swap itself went through -- the pool holds the new version even though the write
        // that should have recorded it never landed.
        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Equal("v-two", after!.Version);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();

        foreach (string directory in new[] { _root, _liveRoot, _stateRoot })
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (DirectoryNotFoundException) { }
        }
    }
}
