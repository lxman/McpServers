using System.Diagnostics;

namespace McpGateway.Supervision;

public sealed class ProcessBackendLauncher : IBackendLauncher
{
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

        return new ProcessHandle(process);
    }

    private sealed class ProcessHandle(Process process) : IBackendHandle
    {
        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!process.HasExited)
                {
                    // The graceful path is /admin/shutdown; this is the backstop for a backend
                    // that ignored it.
                    if (!process.WaitForExit(3000)) process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
