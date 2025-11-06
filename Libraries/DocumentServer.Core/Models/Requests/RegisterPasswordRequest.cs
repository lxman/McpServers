namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to register a password for a specific document
/// </summary>
public class RegisterPasswordRequest
{
    /// <summary>
    /// Full path to the document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Password for the document
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
