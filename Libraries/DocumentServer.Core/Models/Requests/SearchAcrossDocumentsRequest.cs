namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to search across all loaded documents
/// </summary>
public class SearchAcrossDocumentsRequest
{
    /// <summary>
    /// Search query/pattern
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use case-sensitive search (default: false)
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// Maximum number of results per document (default: 10)
    /// </summary>
    public int MaxResultsPerDocument { get; set; } = 10;

    /// <summary>
    /// Use fuzzy matching (default: false)
    /// </summary>
    public bool UseFuzzyMatching { get; set; } = false;

    /// <summary>
    /// Minimum fuzzy match score (0-100, default: 80)
    /// </summary>
    public int FuzzyThreshold { get; set; } = 80;

    /// <summary>
    /// Filter by specific document types (e.g., ["pdf", "docx"])
    /// </summary>
    public List<string> DocumentTypes { get; set; } = [];
}
