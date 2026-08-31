using McpGateway;
using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// The check-then-act window at the top of GetOrStartAsync. A request that has read _holds but not
/// yet inserted into _pool, while ActivateExclusiveAsync installs its hold and then stops the old
/// backend, finds the key gone and spawns a second backend at the old version -- which Replace then
/// orphans: never stopped, invisible to /admin/servers and to the reaper. Two live instances of a
/// server whose whole configuration says there must never be two.
/// </summary>
public sealed class SwapHoldGateTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-holdgate-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly BackendSupervisor _supervisor;

    public SwapHoldGateTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "exclusive": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "pool": "shared", "overlapAllowed": false, "startupTimeoutSeconds": 10
          },
          "perclient": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "pool": "per-client", "overlapAllowed": true, "startupTimeoutSeconds": 30
          },
          "undeployed": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "pool": "shared", "overlapAllowed": false, "startupTimeoutSeconds": 10
          }
        }
        """);

        string statePath = TestState.Write(_root, ("exclusive", "v-one"), ("perclient", "v-one"));

        _supervisor = new BackendSupervisor(
            ManifestStore.Load(manifestPath, statePath),
            _launcher,
            new HealthProbe(new HttpClient(), BackendToken.Mint()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                LiveRegistryPath = Path.Combine(_root, "live"),
                LogPath = Path.Combine(_root, "logs", "gateway-.log"),
                StatePath = statePath,
                RepoRoot = _root
            },
            "backend-token",
            new LiveBackendRegistry(Path.Combine(_root, "live"), NullLogger.Instance),
            NullLogger<BackendSupervisor>.Instance);
    }

    /// <summary>
    /// A hold must not be installable while a request sits between the hold check and the pool
    /// insert. Asserting on a *non*-event, so a slow machine can only make this pass more easily,
    /// never fail spuriously: without the gate HoldAsync completes synchronously, which no amount
    /// of scheduling noise turns into a 250 ms wait.
    /// </summary>
    [Fact]
    public async Task HoldAsync_Waits_ForARequestBetweenTheHoldCheckAndThePoolInsert()
    {
        var atSeam = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        _supervisor.OnHoldChecked = () =>
        {
            if (!atSeam.TrySetResult()) return;

            // Blocks the request exactly inside the window under test.
            release.Task.GetAwaiter().GetResult();
        };

        Task<BackendInstance> request = Task.Run(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("exclusive", ""), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        await atSeam.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Task<IAsyncDisposable> holding = _supervisor.HoldAsync(
            "exclusive", TestContext.Current.CancellationToken);

        await Task.WhenAny(holding, Task.Delay(250, TestContext.Current.CancellationToken));

        try
        {
            Assert.False(holding.IsCompleted,
                "a hold was installed while a request was between the hold check and the pool " +
                "insert; that request will now spawn a backend the swap cannot see");
        }
        finally
        {
            // In a finally so a failing assertion still frees the parked request. Left blocked, it
            // pins a thread pool thread on the seam and the fixture can never tear down -- which
            // is how a failing run strands its temp directory.
            release.SetResult();
        }

        await request;
        await using IAsyncDisposable hold = await holding;
    }

    /// <summary>
    /// After the hold it was waiting on is released, a request has to go back and check again
    /// rather than falling through to the pool insert. Activations queue back to back on
    /// ActivationService's own gate, so the next one's hold can already be installed by the time a
    /// waiter wakes up -- and a waiter that fell through would insert into the pool inside that
    /// swap. The re-check is the only observable difference, because the gate makes the two
    /// operations mutually exclusive once it does happen.
    /// </summary>
    [Fact]
    public async Task GetOrStartAsync_ChecksForAHoldAgain_AfterTheOneItWaitedOnIsReleased()
    {
        var checks = 0;
        var sawFirstCheck = new TaskCompletionSource();

        _supervisor.OnHoldChecked = () =>
        {
            if (Interlocked.Increment(ref checks) == 1) sawFirstCheck.SetResult();
        };

        IAsyncDisposable hold = await _supervisor.HoldAsync(
            "exclusive", TestContext.Current.CancellationToken);

        Task<BackendInstance> request = Task.Run(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("exclusive", ""), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        await sawFirstCheck.Task.WaitAsync(
            TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(request.IsCompleted, "the request did not wait on the hold at all");

        await hold.DisposeAsync();
        await request;

        Assert.Equal(2, Volatile.Read(ref checks));
    }

    /// <summary>
    /// The gate must not serialise ordinary traffic. Overlap servers never take a hold, and even a
    /// non-overlap one must not queue requests behind a start -- concurrent callers for the same
    /// key still share one start rather than waiting their turn to make their own.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequests_StillShareOneStart()
    {
        _launcher.StartDelay = TimeSpan.FromMilliseconds(300);

        var key = new BackendKey("exclusive", "");

        Task<BackendInstance>[] requests =
        [
            _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken),
            _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken),
            _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken)
        ];

        BackendInstance[] served = await Task.WhenAll(requests);

        Assert.Equal(1, _launcher.StartCount);
        Assert.All(served, instance => Assert.Same(served[0], instance));
    }

    /// <summary>
    /// The gate covers the hold check and the pool insert, nothing more. Held across the start
    /// instead, it would queue every client of a server behind whichever one happened to arrive
    /// first -- turning one slow start into a stall for everybody.
    /// </summary>
    [Fact]
    public async Task GetOrStartAsync_DoesNotQueueOneClientBehindAnothersStart()
    {
        _launcher.StartDelay = TimeSpan.FromSeconds(3);

        Task<BackendInstance> code = _supervisor.GetOrStartAsync(
            new BackendKey("perclient", "code"), TestContext.Current.CancellationToken);

        Task<BackendInstance> desktop = _supervisor.GetOrStartAsync(
            new BackendKey("perclient", "desktop"), TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 50 && _launcher.StartCount < 2; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, _launcher.StartCount);
        Assert.False(code.IsCompleted, "the first start finished before the assertion could run");

        await Task.WhenAll(code, desktop);
    }

    /// <summary>
    /// The version lookup now happens inside the gate, so it can throw while the gate is held.
    /// If the finally did not cover it, every later operation on that server -- including a swap --
    /// would block forever on a semaphore nobody owns. WaitAsync turns that into a failure rather
    /// than a hung run.
    /// </summary>
    [Fact]
    public async Task AFailedVersionLookup_StillReleasesTheGate()
    {
        await Assert.ThrowsAsync<BackendStartupException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("undeployed", ""), TestContext.Current.CancellationToken));

        Task<IAsyncDisposable> holding = _supervisor.HoldAsync(
            "undeployed", TestContext.Current.CancellationToken);

        await using IAsyncDisposable hold = await holding.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _supervisor.OnHoldChecked = null;
        await _supervisor.DisposeAsync();

        try { Directory.Delete(_root, recursive: true); }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException) { }
    }
}
