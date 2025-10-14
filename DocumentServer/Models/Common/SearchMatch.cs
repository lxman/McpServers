namespace DocumentServer.Models.Common;

/// <summary>
/// Represents a single match found during a search operation
/// </summary>
public class SearchMatch
{
    /// <summary>
    /// Page number where the match was found (1-based, 0 for non-paginated content)
    /// </summary>
    public int PageNumber { get; set; }
    
    /// <summary>
    /// Context snippet around the match (text surrounding the found term)
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// Character position of the match within the document or page
    /// </summary>
    public int Position { get; set; }
    
    /// <summary>
    /// Line number where the match was found (if applicable)
    /// </summary>
    public int? LineNumber { get; set; }
    
    /// <summary>
    /// The actual matched text
    /// </summary>
    public string MatchedText { get; set; } = string.Empty;
    
    /// <summary>
    /// Relevance score for fuzzy matching (0-100)
    /// </summary>
    public int FuzzyScore { get; set; }
}
