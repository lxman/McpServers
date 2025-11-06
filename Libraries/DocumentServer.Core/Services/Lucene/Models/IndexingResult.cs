namespace DocumentServer.Core.Services.Lucene.Models;

/// <summary>
/// Result of an indexing operation
/// </summary>
public class IndexingResult
{
    /// <summary>
    /// Name of the index that was created or updated
    /// </summary>
    public string IndexName { get; set; } = string.Empty;
    
    /// <summary>
    /// Root directory path that was indexed
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
    
    /// <summary>
    /// When the indexing operation started
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the indexing operation completed
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Total duration of the indexing operation
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    /// <summary>
    /// Total number of documents discovered
    /// </summary>
    public int TotalDocuments { get; set; }
    
    /// <summary>
    /// Number of documents successfully indexed
    /// </summary>
    public int IndexedDocuments { get; set; }
    
    /// <summary>
    /// Number of documents that failed to index
    /// </summary>
    public int FailedDocuments { get; set; }
}
