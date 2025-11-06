namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to search documents in a Lucene index
/// </summary>
/// <param name="IndexName">Name of the index to search</param>
/// <param name="Query">Search query string</param>
/// <param name="MaxResults">Maximum number of results to return (default: 10)</param>
/// <param name="IncludeSnippets">Include context snippets in results (default: true)</param>
public record SearchDocumentsRequest(
    string IndexName, 
    string Query, 
    int MaxResults = 10, 
    bool IncludeSnippets = true);
