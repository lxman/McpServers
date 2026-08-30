using McpGateway.Configuration;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class BackendSupervisorTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-supervisor-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
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
    public async Task GetOrStartAsync_ThrowsWithLogTail_WhenThePortFileNeverArrives()
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

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
