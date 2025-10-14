namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to search a Lucene index
/// </summary>
public class SearchIndexRequest
{
    /// <summary>
    /// Search query string (supports Lucene query syntax)
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results to return (default: 50)
    /// </summary>
    public int MaxResults { get; set; } = 50;

    /// <summary>
    /// Whether to include content snippets in results (default: true)
    /// </summary>
    public bool IncludeSnippets { get; set; } = true;

    /// <summary>
    /// Sort results by: "relevance", "date", "title", "path", "size" (default: "relevance")
    /// </summary>
    public string SortBy { get; set; } = "relevance";

    /// <summary>
    /// Sort in descending order (default: true)
    /// </summary>
    public bool SortDescending { get; set; } = true;

    /// <summary>
    /// Filter by specific file types (e.g., ["pdf", "docx"])
    /// </summary>
    public List<string> FileTypes { get; set; } = [];

    /// <summary>
    /// Filter by documents modified after this date
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Filter by documents modified before this date
    /// </summary>
    public DateTime? EndDate { get; set; }
}
