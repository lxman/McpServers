using DocumentServer.Core.Models.Common;

namespace DocumentServer.Core.Models.Responses;

/// <summary>
/// Response listing all loaded documents
/// </summary>
public class DocumentListResponse
{
    /// <summary>
    /// List of loaded document paths
    /// </summary>
    public List<string> LoadedDocuments { get; set; } = [];

    /// <summary>
    /// Total count of loaded documents
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total memory usage in MB
    /// </summary>
    public double TotalMemoryMB { get; set; }

    /// <summary>
    /// Document details by path
    /// </summary>
    public Dictionary<string, DocumentInfo> Documents { get; set; } = new();
}
