using System.Diagnostics;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// The one test that exercises the real launcher. It spawns McpGateway.TestBackend -- never a real
/// server, and never CodeAssist -- to prove the spawned process is actually adopted into the
/// gateway's job object. BackendJobObjectTests proves what that adoption then buys; this proves the
/// launcher performs it.
/// </summary>
public sealed class ProcessBackendLauncherTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-launcher-" + Guid.NewGuid().ToString("N"));

    private IBackendHandle? _handle;

    private static string TestBackendAssembly => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "McpGateway.TestBackend", "bin", "Debug", "net10.0", "McpGateway.TestBackend.dll"));

    [Fact]
    public void Start_AdoptsTheSpawnedBackendIntoTheGatewayJobObject()
    {
        Assert.True(File.Exists(TestBackendAssembly),
            $"expected a built test backend at {TestBackendAssembly}");

        Directory.CreateDirectory(_root);

        var launcher = new ProcessBackendLauncher(NullLogger<ProcessBackendLauncher>.Instance);

        _handle = launcher.Start(new BackendLaunchRequest(
            "test-backend",
            "v-launcher-test",
            TestBackendAssembly,
            Path.Combine(_root, "port.json"),
            "a-backend-token"));

        using Process spawned = Process.GetProcessById(_handle.ProcessId);

        Assert.True(launcher.Job.IsAvailable, "the launcher created no job object");
        Assert.True(launcher.Job.Contains(spawned),
            "the spawned backend was not adopted into the gateway's job object, so it would " +
            "survive a gateway crash");
    }

    [Fact]
    public void Start_CapturesAProcessIdentityThatSurvivesTeardown()
    {
        Directory.CreateDirectory(_root);

        var launcher = new ProcessBackendLauncher(NullLogger<ProcessBackendLauncher>.Instance);

        _handle = launcher.Start(new BackendLaunchRequest(
            "test-backend",
            "v-launcher-test",
            TestBackendAssembly,
            Path.Combine(_root, "port.json"),
            "a-backend-token"));

        int pid = _handle.ProcessId;
        DateTimeOffset startedAt = _handle.StartedAt;

        Assert.True(pid > 0);

        // Reconciliation compares this against Process.StartTime on a later run, so it has to be
        // the OS's value for this process, not the moment we happened to ask.
        using Process spawned = Process.GetProcessById(pid);
        Assert.Equal(new DateTimeOffset(spawned.StartTime), startedAt);
    }

    public async ValueTask DisposeAsync()
    {
        if (_handle is not null) await _handle.DisposeAsync();

        try { Directory.Delete(_root, recursive: true); }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException) { }
    }
}
