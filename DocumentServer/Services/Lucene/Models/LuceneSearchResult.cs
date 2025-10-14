namespace DocumentServer.Services.Lucene.Models;

/// <summary>
/// Represents a single document result from a Lucene search
/// </summary>
public class LuceneSearchResult
{
    /// <summary>
    /// Full path to the document file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Document title (from metadata or filename)
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Lucene relevance score (higher = more relevant)
    /// </summary>
    public float RelevanceScore { get; set; }
    
    /// <summary>
    /// Type of document (PDF, Word, Excel, PowerPoint, etc.)
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Last modification date of the document
    /// </summary>
    public DateTime ModifiedDate { get; set; }
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// Text snippets showing query terms in context
    /// </summary>
    public List<string> Snippets { get; set; } = [];
}
