namespace McpGateway.Supervision;

public sealed class BackendStartupException(string message, string logTail)
    : Exception(message)
{
    /// <summary>Tail of the backend's log, so a 503 can say why rather than just that.</summary>
    public string LogTail { get; } = logTail;
}
