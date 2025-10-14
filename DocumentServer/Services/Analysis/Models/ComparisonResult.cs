namespace DocumentServer.Services.Analysis.Models;

/// <summary>
/// Results of document comparison
/// </summary>
public class ComparisonResult
{
    /// <summary>
    /// Path to the first document
    /// </summary>
    public string FilePath1 { get; set; } = string.Empty;

    /// <summary>
    /// Path to the second document
    /// </summary>
    public string FilePath2 { get; set; } = string.Empty;

    /// <summary>
    /// Length of first document's text content
    /// </summary>
    public int Document1Length { get; set; }

    /// <summary>
    /// Length of second document's text content
    /// </summary>
    public int Document2Length { get; set; }

    /// <summary>
    /// Word count in first document
    /// </summary>
    public int Document1WordCount { get; set; }

    /// <summary>
    /// Word count in second document
    /// </summary>
    public int Document2WordCount { get; set; }

    /// <summary>
    /// Character-level similarity (0-100%)
    /// </summary>
    public double CharacterSimilarity { get; set; }

    /// <summary>
    /// Number of common words between documents
    /// </summary>
    public int CommonWords { get; set; }

    /// <summary>
    /// Word overlap percentage
    /// </summary>
    public double WordOverlapPercentage { get; set; }

    /// <summary>
    /// Overall similarity score (0-1)
    /// </summary>
    public double OverallSimilarity { get; set; }

    /// <summary>
    /// Whether documents are considered similar (>70% similarity)
    /// </summary>

    /// <summary>
    /// Whether documents are identical (100% similarity)
    /// </summary>
    public bool AreIdentical { get; set; }

    /// <summary>
    /// Overall similarity score as a percentage (0-100)
    /// </summary>
    public double SimilarityScore => OverallSimilarity * 100;

    /// <summary>
    /// Brief summary of the comparison
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// List of significant differences found
    /// </summary>
    public List<string> Differences { get; set; } = [];

    public bool AreSimilar { get; set; }
}