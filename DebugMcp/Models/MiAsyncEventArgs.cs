namespace DebugMcp.Models;

/// <summary>
/// Event arguments for MI async notifications (exec and notify records).
/// </summary>
public class MiAsyncEventArgs : EventArgs
{
    /// <summary>
    /// Session ID this event belongs to.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Type of event (stopped, library-loaded, thread-created, etc.).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Raw MI record string.
    /// </summary>
    public string RawRecord { get; set; } = string.Empty;

    /// <summary>
    /// Parsed data from the record.
    /// </summary>
    public Dictionary<string, string> ParsedData { get; set; } = new();

    /// <summary>
    /// Timestamp when the event was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}