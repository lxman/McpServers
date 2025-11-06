using DocumentServer.Core.Models.Common;

namespace DocumentServer.Core.Models.Responses;

/// <summary>
/// Response from loading a document
/// </summary>
public class LoadDocumentResponse
{
    /// <summary>
    /// Whether the load operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Path to the loaded document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Type of document loaded
    /// </summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>
    /// Whether the document is encrypted
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Whether the document was cached
    /// </summary>
    public bool IsCached { get; set; }

    /// <summary>
    /// Error message if load failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// When the document was loaded
    /// </summary>
    public DateTime LoadedAt { get; set; }
}
