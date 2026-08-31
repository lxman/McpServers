using System.Diagnostics;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// Reconciliation is tested against a real spawned process, not a fake seam. Everything that can
/// actually go wrong here lives in the OS interaction -- Process.GetProcessById on a dead pid,
/// Process.StartTime as an identity check, Kill(entireProcessTree) -- and a seam would only test my
/// own stub of exactly the calls that might be wrong. The stand-in is `cmd /c pause` with its stdin
/// redirected and never written to: it blocks forever, burns no CPU, touches nothing, and is
/// nothing like a backend, let alone CodeAssist.
/// </summary>
public sealed class LiveBackendRegistryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-live-" + Guid.NewGuid().ToString("N"));

    private readonly LiveBackendRegistry _registry;
    private readonly List<Process> _spawned = [];

    public LiveBackendRegistryTests() =>
        _registry = new LiveBackendRegistry(_directory, NullLogger.Instance);

    private Process StartHarmlessProcess()
    {
        Process process = Process.Start(new ProcessStartInfo("cmd.exe", "/c pause")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;

        _spawned.Add(process);
        return process;
    }

    private static LiveBackendRecord RecordFor(Process process, TimeSpan startTimeSkew = default) =>
        new("harmless", string.Empty, "v-test", process.Id, 51000,
            new DateTimeOffset(process.StartTime) + startTimeSkew);

    [Fact]
    public void Record_ThenRead_RoundTripsTheBackend()
    {
        var record = new LiveBackendRecord(
            "code-assist", "", "v-one", 4242, 51000, DateTimeOffset.UtcNow);

        _registry.Record(record);

        LiveBackendRecord only = Assert.Single(_registry.Read());
        Assert.Equal(record.Server, only.Server);
        Assert.Equal(record.Pid, only.Pid);
        Assert.Equal(record.Port, only.Port);
        Assert.Equal(record.Version, only.Version);
    }

    [Fact]
    public void Forget_RemovesTheRecord()
    {
        _registry.Record(new LiveBackendRecord("a", "", "v", 1, 2, DateTimeOffset.UtcNow));
        _registry.Record(new LiveBackendRecord("b", "", "v", 2, 3, DateTimeOffset.UtcNow));

        _registry.Forget(1);

        LiveBackendRecord only = Assert.Single(_registry.Read());
        Assert.Equal(2, only.Pid);
    }

    [Fact]
    public void Reconcile_KillsARecordedProcessThatIsStillAlive()
    {
        Process orphan = StartHarmlessProcess();
        _registry.Record(RecordFor(orphan));

        int killed = _registry.Reconcile();

        Assert.Equal(1, killed);
        Assert.True(orphan.WaitForExit(10_000), "the recorded process was left running");
        Assert.Empty(_registry.Read());
    }

    /// <summary>
    /// Windows reuses pids aggressively, so a stale record can name a process that has nothing to
    /// do with the gateway. The start time is what tells them apart; without that check
    /// reconciliation is a licence to kill an arbitrary process at every gateway start.
    /// </summary>
    [Fact]
    public void Reconcile_LeavesARecycledPidAlone()
    {
        Process innocent = StartHarmlessProcess();

        // Same pid, a start time an hour off: exactly what a recycled pid looks like.
        _registry.Record(RecordFor(innocent, startTimeSkew: TimeSpan.FromHours(-1)));

        int killed = _registry.Reconcile();

        innocent.Refresh();

        Assert.Equal(0, killed);
        Assert.False(innocent.HasExited, "reconciliation killed a process it had not started");
        Assert.Empty(_registry.Read());
    }

    [Fact]
    public void Reconcile_IgnoresARecordWhoseProcessIsAlreadyGone()
    {
        Process gone = StartHarmlessProcess();
        LiveBackendRecord record = RecordFor(gone);

        gone.Kill(entireProcessTree: true);
        gone.WaitForExit(10_000);

        _registry.Record(record);

        Assert.Equal(0, _registry.Reconcile());
        Assert.Empty(_registry.Read());
    }

    [Fact]
    public void Reconcile_OnAnEmptyRegistry_DoesNothing()
    {
        Assert.Equal(0, _registry.Reconcile());
    }

    public void Dispose()
    {
        foreach (Process process in _spawned)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or SystemException)
            {
                // Already gone.
            }

            process.Dispose();
        }

        try { System.IO.Directory.Delete(_directory, recursive: true); }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException) { }
    }
}
