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

    /// <summary>
    /// Published into this test's own directory rather than read out of the project's own
    /// bin/Debug. That path is only populated as a side effect of another class publishing, so on
    /// a cold tree these tests raced it and failed with "No assembly at ... Publish the version
    /// first" -- a failure about test ordering wearing the costume of a launcher bug.
    /// </summary>
    private string PublishTestBackend()
    {
        string output = Path.Combine(_root, "backend");
        TestBackendPublisher.Publish(output, "launcher");

        return Path.Combine(output, "McpGateway.TestBackend.dll");
    }

    [Fact]
    public void Start_AdoptsTheSpawnedBackendIntoTheGatewayJobObject()
    {
        Directory.CreateDirectory(_root);
        string testBackendAssembly = PublishTestBackend();

        Assert.True(File.Exists(testBackendAssembly),
            $"expected a published test backend at {testBackendAssembly}");

        var launcher = new ProcessBackendLauncher(NullLogger<ProcessBackendLauncher>.Instance);

        _handle = launcher.Start(new BackendLaunchRequest(
            "test-backend",
            "v-launcher-test",
            testBackendAssembly,
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
        string testBackendAssembly = PublishTestBackend();

        var launcher = new ProcessBackendLauncher(NullLogger<ProcessBackendLauncher>.Instance);

        _handle = launcher.Start(new BackendLaunchRequest(
            "test-backend",
            "v-launcher-test",
            testBackendAssembly,
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

        // The backend is published INTO _root now, so these are the DLLs the exiting process still
        // has mapped. Its handles close asynchronously after it is asked to stop, so a delete
        // straight afterwards can hit a file that is still open. Retried briefly rather than
        // swallowed, so a temp tree is not left behind on every run.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 20) return;

                await Task.Delay(100);
            }
        }
    }
}
