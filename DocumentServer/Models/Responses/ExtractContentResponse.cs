namespace DocumentServer.Models.Responses;

/// <summary>
/// Response containing extracted document content
/// </summary>
public class ExtractContentResponse
{
    /// <summary>
    /// Whether the extraction succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Path to the document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Extracted text content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Document metadata (if requested)
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Length of extracted content in characters
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// Error message if extraction failed
    /// </summary>
    public string? Error { get; set; }
}
