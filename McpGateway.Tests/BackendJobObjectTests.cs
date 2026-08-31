using System.Diagnostics;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// Proves the primary orphan defence for real, without having to kill the test host: closing the
/// job handle is precisely what the OS does when the gateway process dies, so triggering it by hand
/// exercises the same code path a crash would.
/// <para>
/// The member is `cmd /c pause` with stdin redirected and never written to -- it blocks forever,
/// burns no CPU and is nothing like a backend. No backend, and certainly no CodeAssist, is started
/// anywhere in this file.
/// </para>
/// </summary>
public sealed class BackendJobObjectTests : IDisposable
{
    private readonly List<Process> _spawned = [];

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

    [Fact]
    public void Create_ProducesAJobThatKillsItsMembersWhenClosed()
    {
        using BackendJobObject job = BackendJobObject.Create(NullLogger.Instance);

        Assert.True(job.IsAvailable, "the job object could not be created");

        // Read back from the OS rather than trusted from the call that set it. A P/Invoke struct
        // with a subtly wrong layout can make SetInformationJobObject return success having stored
        // something else entirely, and that failure is silent right up until a gateway crash.
        Assert.True(job.KillsOnClose, "KILL_ON_JOB_CLOSE was not actually stored by the OS");
    }

    [Fact]
    public void TryAssign_PutsTheProcessInThisJob()
    {
        using BackendJobObject job = BackendJobObject.Create(NullLogger.Instance);
        Process child = StartHarmlessProcess();

        Assert.True(job.TryAssign(child));
        Assert.True(job.Contains(child));
    }

    /// <summary>
    /// The whole finding in one assertion: whatever ends the gateway process closes this handle,
    /// and the backends go with it.
    /// </summary>
    [Fact]
    public void ClosingTheJob_KillsEveryProcessAssignedToIt()
    {
        BackendJobObject job = BackendJobObject.Create(NullLogger.Instance);
        Process first = StartHarmlessProcess();
        Process second = StartHarmlessProcess();

        Assert.True(job.TryAssign(first));
        Assert.True(job.TryAssign(second));

        job.Dispose();

        Assert.True(first.WaitForExit(10_000), "closing the job left its first member running");
        Assert.True(second.WaitForExit(10_000), "closing the job left its second member running");
    }

    /// <summary>A process that was never assigned must not be caught in the blast radius.</summary>
    [Fact]
    public void ClosingTheJob_LeavesUnassignedProcessesAlone()
    {
        BackendJobObject job = BackendJobObject.Create(NullLogger.Instance);
        Process assigned = StartHarmlessProcess();
        Process bystander = StartHarmlessProcess();

        Assert.True(job.TryAssign(assigned));

        job.Dispose();

        Assert.True(assigned.WaitForExit(10_000));

        bystander.Refresh();
        Assert.False(bystander.HasExited, "closing the job killed a process it never adopted");
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
    }
}
