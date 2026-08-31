namespace McpGateway.Supervision;

/// <summary>
/// <paramref name="AuthToken"/> is the bearer token the backend will require on every one of its
/// endpoints -- /mcp and /health as well as /admin/shutdown. It reaches the backend in the
/// MCP_SHUTDOWN_TOKEN environment variable, whose name predates that widening.
/// </summary>
public sealed record BackendLaunchRequest(
    string ServerName,
    string Version,
    string AssemblyPath,
    string PortFilePath,
    string AuthToken);

public interface IBackendLauncher
{
    IBackendHandle Start(BackendLaunchRequest request);
}

public interface IBackendHandle : IAsyncDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }

    /// <summary>
    /// When the OS started this process, captured while it is still alive. Half of the identity
    /// check that stops startup reconciliation killing a process that merely inherited the pid.
    /// </summary>
    DateTimeOffset StartedAt { get; }
}
