namespace DebugMcp.Models;

/// <summary>
/// Represents the response to an MI command.
/// </summary>
public class MiResponse
{
    /// <summary>
    /// Token that matches the sent command.
    /// </summary>
    public int Token { get; set; }

    /// <summary>
    /// Whether the command succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result class from the response (done, error, running, etc.).
    /// </summary>
    public string ResultClass { get; set; } = string.Empty;

    /// <summary>
    /// All MI records received for this command.
    /// </summary>
    public List<string> Records { get; set; } = [];

    /// <summary>
    /// Extracted data from the primary result record.
    /// </summary>
    public Dictionary<string, string> Data { get; set; } = new();

    /// <summary>
    /// Error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Get the primary result record (the one with the token).
    /// </summary>
    public string? GetResultRecord()
    {
        return Records.FirstOrDefault(r => r.StartsWith($"{Token}^"));
    }

    /// <summary>
    /// Get all async exec records (*stopped, *running, etc.).
    /// </summary>
    public IEnumerable<string> GetAsyncExecRecords()
    {
        return Records.Where(r => r.StartsWith("*"));
    }

    /// <summary>
    /// Get all async notify records (=library-loaded, =thread-created, etc.).
    /// </summary>
    public IEnumerable<string> GetAsyncNotifyRecords()
    {
        return Records.Where(r => r.StartsWith("="));
    }
}