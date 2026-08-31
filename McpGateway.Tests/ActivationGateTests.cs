using McpGateway;
using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// ActivationService serialises activations so a prune cannot delete the directory a mid-flight
/// swap is about to start from. That is per-SERVER reasoning, but the gate was one semaphore for
/// the whole fleet -- so one slow activation blocked every other server's activate and prune.
/// code-assist alone has a 120 second startup timeout, and Stage 3 adds thirteen more servers.
/// </summary>
public sealed class ActivationGateTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-gate-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly BackendSupervisor _supervisor;
    private readonly ActivationService _activation;

    public ActivationGateTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "slow": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "pool": "shared", "overlapAllowed": true, "startupTimeoutSeconds": 30
          },
          "quick": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "pool": "shared", "overlapAllowed": true, "startupTimeoutSeconds": 30
          }
        }
        """);

        string statePath = TestState.Write(_root, ("slow", "v-one"), ("quick", "v-one"));
        ManifestStore manifest = ManifestStore.Load(manifestPath, statePath);

        _supervisor = new BackendSupervisor(
            manifest, _launcher, new HealthProbe(new HttpClient(), BackendToken.Mint()),
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

        _activation = new ActivationService(
            _supervisor, manifest, NullLogger<ActivationService>.Instance);
    }

    [Fact]
    public async Task ActivatingOneServer_DoesNotBlockAnother()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // Both need a live backend, or activation has nothing to swap and never starts anything.
        await _supervisor.GetOrStartAsync(new BackendKey("slow", ""), token);
        await _supervisor.GetOrStartAsync(new BackendKey("quick", ""), token);

        // Captured by the launcher when the start begins, so clearing it afterwards leaves the
        // slow activation slow while letting the quick one through at full speed.
        _launcher.StartDelay = TimeSpan.FromSeconds(3);
        Task<ActivationResult> slow = _activation.ActivateAsync("slow", "v-two", token);

        await Task.Delay(TimeSpan.FromMilliseconds(300), token);
        _launcher.StartDelay = TimeSpan.Zero;

        ActivationResult quick = await _activation.ActivateAsync("quick", "v-two", token);

        // The assertion that matters, and it is about ordering rather than elapsed time: on one
        // fleet-wide gate the quick activation could not have returned until the slow one released.
        Assert.False(slow.IsCompleted);
        Assert.True(quick.Succeeded, quick.Error);

        Assert.True((await slow).Succeeded);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
    }
}
