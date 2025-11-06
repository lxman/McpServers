using System.Text.Json.Serialization;

namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to extract content from a document
/// </summary>
public class ExtractContentRequest
{
    /// <summary>
    /// Full path to the document file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include metadata in the extraction (default: false)
    /// </summary>
    public bool IncludeMetadata { get; set; } = false;

    /// <summary>
    /// Starting page number (1-based, null = from beginning)
    /// </summary>
    [JsonPropertyName("startPage")]
    public int? StartPage { get; set; }

    /// <summary>
    /// Ending page number (1-based, inclusive, null = to end)
    /// </summary>
    [JsonPropertyName("endPage")]
    public int? EndPage { get; set; }

    /// <summary>
    /// Maximum number of pages to extract (alternative to StartPage/EndPage)
    /// </summary>
    [JsonPropertyName("maxPages")]
    public int? MaxPages { get; set; }
}
