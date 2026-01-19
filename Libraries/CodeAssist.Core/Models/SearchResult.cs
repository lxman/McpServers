namespace CodeAssist.Core.Models;

/// <summary>
/// Represents a search result from the vector database.
/// </summary>
public sealed record SearchResult
{
    /// <summary>
    /// The matching code chunk.
    /// </summary>
    public required CodeChunk Chunk { get; init; }

    /// <summary>
    /// Similarity score (0.0 to 1.0, higher is more similar).
    /// </summary>
    public required float Score { get; init; }
}

/// <summary>
/// Collection of search results with metadata.
/// </summary>
public sealed record SearchResponse
{
    /// <summary>
    /// The original query text.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Matching results ordered by relevance.
    /// </summary>
    public required IReadOnlyList<SearchResult> Results { get; init; }

    /// <summary>
    /// Total time taken for the search.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Name of the repository that was searched.
    /// </summary>
    public required string RepositoryName { get; init; }
}
