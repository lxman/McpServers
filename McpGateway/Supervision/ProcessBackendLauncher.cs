using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

public sealed class ProcessBackendLauncher(ILogger<ProcessBackendLauncher> logger) : IBackendLauncher
{
    /// <summary>
    /// Deliberately never disposed. Closing the job is what kills the backends, so the only correct
    /// moment is gateway process exit -- which the OS handles for us, and which is exactly the
    /// point: it happens on a crash too, not just on a graceful shutdown.
    /// </summary>
    private readonly BackendJobObject _job = BackendJobObject.Create(logger);

    /// <summary>Verification only: lets a test confirm a spawned backend was adopted.</summary>
    internal BackendJobObject Job => _job;

    public IBackendHandle Start(BackendLaunchRequest request)
    {
        if (!File.Exists(request.AssemblyPath))
        {
            throw new BackendStartupException(
                $"No assembly at {request.AssemblyPath}. Publish the version first.",
                string.Empty);
        }

        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(request.AssemblyPath)!,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        info.ArgumentList.Add(request.AssemblyPath);
        info.ArgumentList.Add("--mcp-port-file");
        info.ArgumentList.Add(request.PortFilePath);

        info.Environment["MCP_SERVER_NAME"] = request.ServerName;
        info.Environment["MCP_SERVER_VERSION"] = request.Version;

        // Historical name, widened meaning: this is the token the backend requires on /mcp and
        // /health too, not just /admin/shutdown. Kept as-is so a version directory published
        // before the widening still receives it. Mcp.Hosting.Core reads it into
        // McpHostOptions.AuthToken.
        info.Environment["MCP_SHUTDOWN_TOKEN"] = request.AuthToken;

        Process process = Process.Start(info)
            ?? throw new BackendStartupException(
                $"Could not start {request.AssemblyPath}.", string.Empty);

        // Immediately, so the window in which the backend is unadopted is as short as it can be
        // without creating the process suspended.
        if (!_job.TryAssign(process))
        {
            logger.LogCritical(
                "Could not assign {Server} (pid {Pid}) to the gateway's job object (win32 error " +
                "{Error}). It will survive a non-graceful gateway exit as an orphan, and startup " +
                "reconciliation is then the only thing that will clean it up",
                request.ServerName, process.Id, Marshal.GetLastWin32Error());
        }

        return new ProcessHandle(process);
    }

    private sealed class ProcessHandle(Process process) : IBackendHandle
    {
        // Captured now: Process.StartTime throws once the process has been reaped, and the whole
        // point of recording it is to still know it after the process is gone.
        public DateTimeOffset StartedAt { get; } = new(process.StartTime);

        // Also captured: Process.Id throws once the Process object has been disposed, and the
        // registry has to be able to name the pid it is clearing *after* teardown.
        public int ProcessId { get; } = process.Id;

        public bool HasExited => process.HasExited;

        /// <summary>How long a backend gets to exit on its own before it is killed.</summary>
        private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(3);

        /// <summary>How long the kill itself gets to be reaped before we stop waiting on it.</summary>
        private static readonly TimeSpan ReapTimeout = TimeSpan.FromSeconds(5);

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!process.HasExited)
                {
                    // The graceful path is /admin/shutdown; this is the backstop for a backend
                    // that ignored it.
                    //
                    // Awaited rather than process.WaitForExit(3000). That overload blocks a thread
                    // pool thread for the whole grace period and no CancellationToken can reach
                    // into it, so a caller's timeout could not bound it -- and with a backend per
                    // session across fourteen servers, shutdown disposes a lot of these at once.
                    using var grace = new CancellationTokenSource(GracePeriod);
                    try
                    {
                        await process.WaitForExitAsync(grace.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }

                // Bounded too. TerminateProcess does not fail in practice, but an unbounded await
                // here is an unbounded await on gateway shutdown, which is where this runs.
                using var reap = new CancellationTokenSource(ReapTimeout);
                await process.WaitForExitAsync(reap.Token);
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
            catch (OperationCanceledException)
            {
                // Killed and still not reaped. There is nothing further to try, and holding
                // shutdown open on it helps nobody.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
