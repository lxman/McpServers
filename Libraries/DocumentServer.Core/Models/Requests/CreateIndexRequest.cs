namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to create a new Lucene index from a directory of documents
/// </summary>
public class CreateIndexRequest
{
    /// <summary>
    /// Name for the index (must be unique)
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Root directory path containing documents to index
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// File patterns to include (e.g., "*.pdf,*.docx"). Leave empty for all supported types.
    /// </summary>
    public string? IncludePatterns { get; set; }

    /// <summary>
    /// Whether to search subdirectories recursively (default: true)
    /// </summary>
    public bool Recursive { get; set; } = true;
}
