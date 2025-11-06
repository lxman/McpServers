namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to load a document into memory
/// </summary>
public class LoadDocumentRequest
{
    /// <summary>
    /// Full path to the document file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Password for encrypted documents (optional)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether to cache the loaded document (default: true)
    /// </summary>
    public bool Cache { get; set; } = true;
}
