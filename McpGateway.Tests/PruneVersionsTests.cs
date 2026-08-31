using McpGateway.Configuration;
using McpGateway.Security;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class PruneVersionsTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-prune-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly BackendSupervisor _supervisor;
    private readonly ActivationService _activation;

    public PruneVersionsTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "prunable": {
            "project": "P/P.csproj", "assembly": "P.dll", "deployRoot": "deploy/p",
            "activeVersion": "v-two", "pool": "per-client",
            "startupTimeoutSeconds": 10
          }
        }
        """);

        ManifestStore manifest = ManifestStore.Load(manifestPath);
        _supervisor = new BackendSupervisor(
            manifest,
            _launcher,
            new HealthProbe(new HttpClient(), BackendToken.Mint()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                RepoRoot = _root
            },
            "shutdown-token",
            NullLogger<BackendSupervisor>.Instance);

        _activation = new ActivationService(
            _supervisor, manifest, NullLogger<ActivationService>.Instance);
    }

    private string VersionDirectory(string version) =>
        Path.Combine(_root, "deploy", "p", version);

    private void CreateVersionDirectory(string version) =>
        Directory.CreateDirectory(VersionDirectory(version));

    [Fact]
    public async Task PruneVersionsAsync_DeletesAVersionThatIsNeitherActiveNorLive()
    {
        CreateVersionDirectory("v-one");
        CreateVersionDirectory("v-two"); // the manifest's activeVersion

        IReadOnlyList<string> pruned = await _supervisor.PruneVersionsAsync(
            "prunable", TestContext.Current.CancellationToken);

        Assert.Contains("v-one", pruned);
        Assert.False(Directory.Exists(VersionDirectory("v-one")));
    }

    [Fact]
    public async Task PruneVersionsAsync_KeepsTheActiveVersion()
    {
        CreateVersionDirectory("v-one");
        CreateVersionDirectory("v-two"); // the manifest's activeVersion

        IReadOnlyList<string> pruned = await _supervisor.PruneVersionsAsync(
            "prunable", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("v-two", pruned);
        Assert.True(Directory.Exists(VersionDirectory("v-two")));
    }

    [Fact]
    public async Task PruneVersionsAsync_KeepsALiveVersion_EvenWhenTheManifestPointsElsewhere()
    {
        CreateVersionDirectory("v-one");
        CreateVersionDirectory("v-two"); // the manifest's activeVersion, but nothing is running it

        // A backend is still running v-one even though the manifest already claims v-two -- e.g. a
        // held request served from the old version while the manifest write raced ahead. Pruning
        // must not delete the directory backing a version something is actually running, no matter
        // what the manifest currently says.
        var key = new BackendKey("prunable", "code");
        BackendInstance liveOnOldVersion = await _supervisor.StartDetachedAsync(
            key, "v-one", TestContext.Current.CancellationToken);
        _supervisor.Replace(key, liveOnOldVersion);

        IReadOnlyList<string> pruned = await _supervisor.PruneVersionsAsync(
            "prunable", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("v-one", pruned);
        Assert.True(Directory.Exists(VersionDirectory("v-one")));
    }

    [Fact]
    public async Task PruneVersionsAsync_LeavesALockedDirectoryInPlace_RatherThanThrowing()
    {
        CreateVersionDirectory("v-locked");
        CreateVersionDirectory("v-two"); // the manifest's activeVersion

        string lockedFile = Path.Combine(VersionDirectory("v-locked"), "locked.txt");
        File.WriteAllText(lockedFile, "locked");

        // Holding the file open with no sharing simulates something still having a handle into the
        // directory -- Directory.Delete(recursive: true) fails with IOException on Windows here.
        await using var stream = new FileStream(
            lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);

        IReadOnlyList<string> pruned = await _supervisor.PruneVersionsAsync(
            "prunable", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("v-locked", pruned);
        Assert.True(Directory.Exists(VersionDirectory("v-locked")));
    }

    [Fact]
    public async Task PruneAsync_DelegatesToPruneVersionsAsync()
    {
        // ActivationService.PruneAsync exists so pruning takes the same gate an activation holds
        // -- the fix for the race where a concurrent prune could delete the deploy directory a
        // mid-flight swap is about to start from. This only proves the wrapper actually delegates
        // rather than being dead code the /admin/prune endpoint calls into for no effect; the
        // gate-ordering property itself is exercised separately below.
        CreateVersionDirectory("v-one");
        CreateVersionDirectory("v-two"); // the manifest's activeVersion

        IReadOnlyList<string> pruned = await _activation.PruneAsync(
            "prunable", TestContext.Current.CancellationToken);

        Assert.Contains("v-one", pruned);
        Assert.False(Directory.Exists(VersionDirectory("v-one")));
    }

    [Fact]
    public async Task PruneAsync_WaitsForAnInFlightActivation_RatherThanRunningConcurrently()
    {
        CreateVersionDirectory("v-one"); // orphan; gives the eventual prune something to remove
        CreateVersionDirectory("v-two"); // the manifest's activeVersion

        var key = new BackendKey("prunable", "code");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        // Hold the gate with a slow-motion activation, the same pattern
        // Activate_HoldsArrivingRequestsRatherThanRefusingThem already uses.
        _launcher.StartDelay = TimeSpan.FromMilliseconds(400);
        Task<ActivationResult> activating = _activation.ActivateAsync(
            "prunable", "v-three", TestContext.Current.CancellationToken);

        await Task.Delay(150, TestContext.Current.CancellationToken);

        // Issued while the activation above is still mid-swap and holding the gate.
        Task<IReadOnlyList<string>> pruning = _activation.PruneAsync(
            "prunable", TestContext.Current.CancellationToken);

        // Give the call a moment to reach (and block on) the gate, then confirm it is genuinely
        // still blocked -- not racing the activation, waiting for it.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(pruning.IsCompleted,
            "PruneAsync completed while an activation was still holding the gate -- it is not " +
            "actually serialized against activations.");

        ActivationResult activationResult = await activating;
        Assert.True(activationResult.Succeeded, activationResult.Error);

        IReadOnlyList<string> pruned = await pruning;
        Assert.Contains("v-one", pruned);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
    }
}
