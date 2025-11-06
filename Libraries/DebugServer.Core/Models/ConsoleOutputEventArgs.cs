namespace DebugServer.Core.Models;

/// <summary>
/// Event arguments for console output from the debugged program.
/// </summary>
public class ConsoleOutputEventArgs : EventArgs
{
    /// <summary>
    /// Session ID this output belongs to.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Stream type: '~' (console), '@' (target), '&' (log).
    /// </summary>
    public char StreamType { get; set; }

    /// <summary>
    /// Output content (unescaped).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the output was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Get a description of the stream type.
    /// </summary>
    public string StreamTypeDescription => StreamType switch
    {
        '~' => "Console",
        '@' => "Target",
        '&' => "Log",
        _ => "Unknown"
    };
}