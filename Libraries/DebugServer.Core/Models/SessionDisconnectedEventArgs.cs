namespace DebugServer.Core.Models;

/// <summary>
/// Event arguments for session disconnection.
/// </summary>
public class SessionDisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// Session ID that was disconnected.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Reason for disconnection.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the disconnection occurred.
    /// </summary>
    public DateTime DisconnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this was an expected disconnection (user requested).
    /// </summary>
    public bool IsExpected { get; set; }
}