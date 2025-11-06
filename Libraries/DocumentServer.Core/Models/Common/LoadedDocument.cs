namespace DocumentServer.Core.Models.Common;

/// <summary>
/// Represents a document that has been loaded into memory
/// </summary>
public class LoadedDocument
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
    /// Timestamp when the document was loaded into memory
    /// </summary>
    public DateTime LoadedAt { get; set; }
    
    /// <summary>
    /// The actual document object (type varies by format)
    /// For PDFs: PdfDocument
    /// For Excel: XLWorkbook
    /// For Word: WordprocessingDocument
    /// For PowerPoint: Presentation
    /// </summary>
    public object? DocumentObject { get; set; }
    
    /// <summary>
    /// Approximate memory size of the loaded document in bytes
    /// </summary>
    public long MemorySizeBytes { get; set; }
    
    /// <summary>
    /// Indicates if the document was loaded with a password
    /// </summary>
    public bool WasPasswordProtected { get; set; }
    
    /// <summary>
    /// Number of times this document has been accessed since loading
    /// </summary>
    public int AccessCount { get; set; }
    
    /// <summary>
    /// Timestamp of the last access to this document
    /// </summary>
    public DateTime LastAccessedAt { get; set; }
}
