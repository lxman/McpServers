using McpGateway.Configuration;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class PruneVersionsTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-prune-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly BackendSupervisor _supervisor;

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

        _supervisor = new BackendSupervisor(
            ManifestStore.Load(manifestPath),
            _launcher,
            new HealthProbe(new HttpClient()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                RepoRoot = _root
            },
            "shutdown-token",
            NullLogger<BackendSupervisor>.Instance);
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

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }
    }
}
