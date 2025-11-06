namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to search across all loaded documents
/// </summary>
public class SearchAllDocumentsRequest
{
    /// <summary>
    /// Search query text
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results per document (default: 10)
    /// </summary>
    public int MaxResultsPerDocument { get; set; } = 10;

    /// <summary>
    /// Maximum total results across all documents (default: 100)
    /// </summary>
    public int MaxTotalResults { get; set; } = 100;

    /// <summary>
    /// Use fuzzy matching (allows minor typos, default: false)
    /// </summary>
    public bool UseFuzzyMatch { get; set; } = false;

    /// <summary>
    /// Minimum fuzzy match score (0-100, default: 70)
    /// </summary>
    public int FuzzyThreshold { get; set; } = 70;

    /// <summary>
    /// Filter by document types (e.g., ["pdf", "docx"]. Empty = all types)
    /// </summary>
    public List<string> DocumentTypes { get; set; } = [];
}
