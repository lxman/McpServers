namespace DocumentServer.Core.Models.Responses;

/// <summary>
/// Response from comparing two documents
/// </summary>
public class CompareDocumentsResponse
{
    /// <summary>
    /// Whether the comparison succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Path to the first document
    /// </summary>
    public string FilePath1 { get; set; } = string.Empty;

    /// <summary>
    /// Path to the second document
    /// </summary>
    public string FilePath2 { get; set; } = string.Empty;

    /// <summary>
    /// Whether the documents are identical
    /// </summary>
    public bool AreIdentical { get; set; }

    /// <summary>
    /// Similarity score (0.0 to 1.0)
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// List of differences found
    /// </summary>
    public List<string> Differences { get; set; } = [];

    /// <summary>
    /// Summary of the comparison
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Error message if comparison failed
    /// </summary>
    public string? Error { get; set; }
}
