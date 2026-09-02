using System.Diagnostics;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// The guard's whole purpose is to stop a SECOND GATEWAY PROCESS, and nothing in-process can show
/// that: the <c>HeldInThisProcess</c> set would refuse a second Acquire on its own even if the named
/// mutex were never created. So every test here puts the other party in a real second process and
/// drives the OS object across the boundary.
/// <para>
/// A second process is spawned rather than a second gateway because the named mutex is the entire
/// mechanism under test -- who holds it and who is refused. Standing up a whole gateway would prove
/// the same thing far more slowly, and would fail on the port before it ever reached the guard.
/// </para>
/// </summary>
public sealed class SingleInstanceGuardAcrossProcessesTests
{
    private static readonly NullLogger<SingleInstanceGuard> Logger = new();

    /// <summary>A name no other test or live gateway can collide with.</summary>
    private static string UniqueName() =>
        SingleInstanceGuard.NameFor(Path.Combine(Path.GetTempPath(), $"live-{Guid.NewGuid():N}"));

    [Fact]
    public async Task A_second_process_is_refused_the_name_this_one_holds()
    {
        string name = UniqueName();

        using SingleInstanceGuard guard = SingleInstanceGuard.Acquire(name, Logger);

        Assert.Equal("REFUSED", await ProbeAsync(name, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The direction that matters: our code is the second gateway, and it must refuse rather than
    /// proceed to reconcile the running one's backends as orphans.
    /// </summary>
    [Fact]
    public async Task Acquire_refuses_when_another_process_already_holds_the_name()
    {
        string name = UniqueName();

        using Process holder = await StartHolderAsync(name, TestContext.Current.CancellationToken);

        try
        {
            InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
                () => SingleInstanceGuard.Acquire(name, Logger));

            Assert.Contains("Another gateway is already running", refusal.Message);
            Assert.Contains(name, refusal.Message);
        }
        finally
        {
            holder.Kill(entireProcessTree: true);
        }
    }

    /// <summary>
    /// Released on Dispose, not merely on exit -- otherwise a gateway that shut down cleanly would
    /// still lock out its own replacement until the process died.
    /// </summary>
    [Fact]
    public async Task Disposing_the_guard_frees_the_name_for_the_next_process()
    {
        string name = UniqueName();

        // No await between Acquire and Dispose: mutex ownership is thread-affine, and releasing on
        // the acquiring thread is what makes this a clean release rather than an abandonment.
        using (SingleInstanceGuard.Acquire(name, Logger))
        {
        }

        Assert.Equal("ACQUIRED", await ProbeAsync(name, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Takes the name in another process and lets go immediately, reporting which happened.
    /// An abandoned mutex counts as ACQUIRED: the name was free to take, which is the question.
    /// </summary>
    private static async Task<string> ProbeAsync(string name, CancellationToken cancellationToken)
    {
        using Process process = StartPowerShell($$"""
            $m = New-Object System.Threading.Mutex($false, '{{name}}')
            try { $got = $m.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $got = $true }
            if ($got) { [Console]::Out.WriteLine('ACQUIRED'); $m.ReleaseMutex() }
            else { [Console]::Out.WriteLine('REFUSED') }
            [Console]::Out.Flush()
            """);

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return output.Trim();
    }

    /// <summary>
    /// Takes the name in another process and HOLDS it, returning once it has confirmed it did.
    /// The caller kills it.
    /// </summary>
    private static async Task<Process> StartHolderAsync(string name, CancellationToken cancellationToken)
    {
        Process process = StartPowerShell($$"""
            $m = New-Object System.Threading.Mutex($false, '{{name}}')
            try { $got = $m.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $got = $true }
            if ($got) { [Console]::Out.WriteLine('ACQUIRED') } else { [Console]::Out.WriteLine('REFUSED') }
            [Console]::Out.Flush()
            Start-Sleep -Seconds 120
            """);

        try
        {
            // Read the confirmation line before returning, so the caller cannot race the child's
            // acquisition and get a pass for the wrong reason.
            string? line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            Assert.Equal("ACQUIRED", line?.Trim());
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            process.Dispose();
            throw;
        }

        return process;
    }

    /// <summary>
    /// Pinned to the System32 copy for the same reason the identity tests pin curl: whatever
    /// "powershell" resolves to on PATH is not necessarily a shell this script parses under.
    /// </summary>
    private static Process StartPowerShell(string script)
    {
        string powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");

        var startInfo = new ProcessStartInfo(powershell)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);

        return Process.Start(startInfo)
               ?? throw new InvalidOperationException("Could not start the second process");
    }
}
