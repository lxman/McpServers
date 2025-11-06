// ReSharper disable CheckNamespace
#pragma warning disable CA1050

/// <summary>
/// Represents the state of a pending MI command.
/// </summary>
public enum CommandState
{
    /// <summary>
    /// Command has been sent, awaiting response.
    /// </summary>
    Sent,

    /// <summary>
    /// Received ^running, waiting for *stopped.
    /// </summary>
    Running,

    /// <summary>
    /// Received *stopped, waiting for (gdb) prompt.
    /// </summary>
    Stopped,

    /// <summary>
    /// Received ^done, waiting for (gdb) prompt.
    /// </summary>
    DoneOk,

    /// <summary>
    /// Received ^error, waiting for (gdb) prompt.
    /// </summary>
    Error,

    /// <summary>
    /// Received (gdb) prompt, command is complete.
    /// </summary>
    Complete
}

public enum DebugSessionState
{
    Created,
    Launching,
    Running,
    Paused,
    Stopped,
    Exited,
    Error,
    Terminated
}