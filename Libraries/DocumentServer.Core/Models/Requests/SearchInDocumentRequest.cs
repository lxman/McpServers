namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to search within a specific loaded document
/// </summary>
public class SearchInDocumentRequest
{
    /// <summary>
    /// Path to the document to search
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Search query/pattern
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use case-sensitive search (default: false)
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Maximum number of results to return (default: 100)
    /// </summary>
    public int MaxResults { get; set; } = 100;

    /// <summary>
    /// Use fuzzy matching (default: false)
    /// </summary>
    public bool UseFuzzyMatching { get; set; } = false;

    /// <summary>
    /// Minimum fuzzy match score (0-100, default: 80)
    /// </summary>
    public int FuzzyThreshold { get; set; } = 80;
}
