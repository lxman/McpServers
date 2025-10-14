namespace DocumentServer.Services.Analysis.Models;

/// <summary>
/// Enriched metadata containing both file system and document metadata
/// </summary>
public class EnrichedMetadata
{
    /// <summary>
    /// Full path to the document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// File name
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File extension
    /// </summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// File creation timestamp
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// File modification timestamp
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// File access timestamp
    /// </summary>
    public DateTime Accessed { get; set; }

    /// <summary>
    /// Document type
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Whether document is encrypted
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Number of pages (for paginated documents)
    /// </summary>
    public int? PageCount { get; set; }

    /// <summary>
    /// Document author
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Document title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Document creation date (from metadata)
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Content length in characters
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// Word count
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// Line count
    /// </summary>
    public int LineCount { get; set; }

    /// <summary>
    /// All document metadata as key-value pairs
    /// </summary>
    public Dictionary<string, string> DocumentMetadata { get; set; } = new();
}