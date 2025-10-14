namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to compare two documents
/// </summary>
public class CompareDocumentsRequest
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
    /// Type of comparison: "content", "structure", "metadata" (default: "content")
    /// </summary>
    public string ComparisonType { get; set; } = "content";

    /// <summary>
    /// Whether to include detailed differences (default: true)
    /// </summary>
    public bool IncludeDetails { get; set; } = true;
}
