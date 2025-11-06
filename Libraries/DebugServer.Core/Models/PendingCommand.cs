namespace DebugServer.Core.Models;

/// <summary>
/// Represents a command that has been sent and is awaiting response.
/// </summary>
public class PendingCommand
{
    /// <summary>
    /// Unique token assigned to this command.
    /// </summary>
    public int Token { get; set; }

    /// <summary>
    /// The MI command string (without token prefix).
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// TaskCompletionSource to signal command completion.
    /// </summary>
    public TaskCompletionSource<MiResponse> CompletionSource { get; set; } = null!;

    /// <summary>
    /// All MI records received for this command (result, async, etc.).
    /// </summary>
    public List<string> AccumulatedRecords { get; set; } = [];

    /// <summary>
    /// When the command was sent.
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// Whether this is an execution command that expects ^running then *stopped.
    /// </summary>
    public bool ExpectsRunningState { get; set; }

    /// <summary>
    /// Current state of the command.
    /// </summary>
    public CommandState State { get; set; } = CommandState.Sent;
}