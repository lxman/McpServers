namespace DocumentServer.Core.Models.Common;

/// <summary>
/// Represents search results from a single document
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Full path to the document where matches were found
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of document
    /// </summary>
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// List of matches found in this document
    /// </summary>
    public List<SearchMatch> Matches { get; set; } = [];
    
    /// <summary>
    /// Total number of matches found
    /// </summary>
    public int MatchCount => Matches.Count;
    
    /// <summary>
    /// Overall relevance score for this document (for ranked search results)
    /// </summary>
    public float RelevanceScore { get; set; }
    
    /// <summary>
    /// Additional metadata about the document
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
