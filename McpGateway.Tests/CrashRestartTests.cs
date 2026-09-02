using McpGateway;
using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// A backend found dead is evicted and restarted on the next request. Nothing bounded how often:
/// a backend that dies the instant it starts sent GetOrStartAsync round its loop forever, spawning
/// processes as fast as the machine allowed, inside a single request that never returned.
/// </summary>
public sealed class CrashRestartTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-crash-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly BackendSupervisor _supervisor;

    public CrashRestartTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "flaps": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "pool": "shared", "overlapAllowed": true, "startupTimeoutSeconds": 10
          }
        }
        """);

        string statePath = TestState.Write(_root, ("flaps", "v-one"));

        _supervisor = new BackendSupervisor(
            ManifestStore.Load(manifestPath, statePath), _launcher,
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
    /// Bounded by a wall clock rather than by awaiting the call directly: unbounded, the call never
    /// returns at all, and a test that hangs is a worse failure report than one that fails.
    /// </summary>
    [Fact]
    public async Task ABackendThatDiesOnEveryStart_GivesUpInsteadOfLoopingForever()
    {
        _launcher.ExitsImmediately = true;

        Task<BackendInstance> start = _supervisor.GetOrStartAsync(
            new BackendKey("flaps", ""), CancellationToken.None);

        Task finished = await Task.WhenAny(
            start, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.True(
            ReferenceEquals(finished, start),
            "GetOrStartAsync never returned; the crash-restart loop is unbounded.");

        await Assert.ThrowsAsync<BackendStartupException>(() => start);

        // The cap, not just the giving up: a handful of attempts, not hundreds.
        Assert.InRange(_launcher.StartCount, 1, 6);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
    }
}
