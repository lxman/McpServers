namespace DocumentServer.Core.Models.Responses;

/// <summary>
/// Response listing all available indexes
/// </summary>
public class IndexListResponse
{
    /// <summary>
    /// Names of all discovered indexes
    /// </summary>
    public List<string> IndexNames { get; set; } = [];

    /// <summary>
    /// Total count of indexes
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Number of indexes currently loaded in memory
    /// </summary>
    public int LoadedInMemoryCount { get; set; }
}
