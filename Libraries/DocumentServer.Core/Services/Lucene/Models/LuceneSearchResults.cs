namespace DocumentServer.Core.Services.Lucene.Models;

/// <summary>
/// Container for Lucene search results with query metadata
/// </summary>
public class LuceneSearchResults
{
    /// <summary>
    /// The search query that was executed
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of matching documents found
    /// </summary>
    public long TotalHits { get; set; }
    
    /// <summary>
    /// Time taken to execute the search in milliseconds
    /// </summary>
    public double SearchTimeMs { get; set; }
    
    /// <summary>
    /// List of individual search results (paginated if applicable)
    /// </summary>
    public List<LuceneSearchResult> Results { get; set; } = [];
    
    /// <summary>
    /// Count of results by file type
    /// </summary>
    public Dictionary<string, int> FileTypeCounts { get; set; } = new();
    
    /// <summary>
    /// Count of results by directory
    /// </summary>
    public Dictionary<string, int> DirectoryCounts { get; set; } = new();

}