namespace McpGateway.Supervision;

public sealed record BackendLaunchRequest(
    string ServerName,
    string Version,
    string AssemblyPath,
    string PortFilePath,
    string ShutdownToken);

public interface IBackendLauncher
{
    IBackendHandle Start(BackendLaunchRequest request);
}

public interface IBackendHandle : IAsyncDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
}
