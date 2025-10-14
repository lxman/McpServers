namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to extract content from a document
/// </summary>
public class ExtractContentRequest
{
    /// <summary>
    /// Full path to the document file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include metadata in the extraction (default: false)
    /// </summary>
    public bool IncludeMetadata { get; set; } = false;
}
