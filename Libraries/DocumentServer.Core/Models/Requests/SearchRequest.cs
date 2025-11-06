namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to search for text in documents
/// </summary>
/// <param name="SearchTerm">Text to search for</param>
/// <param name="FuzzySearch">Enable fuzzy matching (default: false)</param>
/// <param name="MaxResults">Maximum number of results to return (default: 50)</param>
public record SearchRequest(
    string SearchTerm, 
    bool FuzzySearch = false, 
    int MaxResults = 50);
