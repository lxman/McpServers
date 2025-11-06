namespace DocumentServer.Core.Models.Responses;

/// <summary>
/// Response from testing a query
/// </summary>
public class TestQueryResponse
{
    /// <summary>
    /// The query that was tested
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Number of documents that would match this query
    /// </summary>
    public int TotalHits { get; set; }

    /// <summary>
    /// Whether the query syntax is valid
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Error message if query is invalid
    /// </summary>
    public string? ErrorMessage { get; set; }
}
