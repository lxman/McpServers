namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to register a password pattern for multiple files
/// </summary>
public class RegisterPasswordPatternRequest
{
    /// <summary>
    /// File pattern (e.g., "*confidential*.pdf", "*.xlsx")
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Password to use for files matching the pattern
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
