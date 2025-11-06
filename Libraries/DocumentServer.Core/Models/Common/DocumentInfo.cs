namespace DocumentServer.Core.Models.Common;

/// <summary>
/// Contains metadata and information about a document
/// </summary>
public class DocumentInfo
{
    /// <summary>
    /// Full path to the document file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of document
    /// </summary>
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// Size of the file in bytes
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// Last modification timestamp of the file
    /// </summary>
    public DateTime LastModified { get; set; }
    
    /// <summary>
    /// Indicates if the document is password-protected/encrypted
    /// </summary>
    public bool IsEncrypted { get; set; }
    
    /// <summary>
    /// Additional metadata extracted from the document
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
    
    /// <summary>
    /// Total number of pages (for multi-page documents)
    /// </summary>
    public int? PageCount { get; set; }
    
    /// <summary>
    /// Document author (if available in metadata)
    /// </summary>
    public string? Author { get; set; }
    
    /// <summary>
    /// Document title (if available in metadata)
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Document creation date (if available in metadata)
    /// </summary>
    public DateTime? CreatedDate { get; set; }
}
