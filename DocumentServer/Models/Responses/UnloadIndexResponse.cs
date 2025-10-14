namespace DocumentServer.Models.Responses;

/// <summary>
/// Response from unloading an index or indexes
/// </summary>
public class UnloadIndexResponse
{
    /// <summary>
    /// Whether the unload operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of indexes unloaded
    /// </summary>
    public int UnloadedCount { get; set; }

    /// <summary>
    /// Message describing the result
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
