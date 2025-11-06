namespace AzureServer.Core.Common.Models;

/// <summary>
/// Pagination metadata for paginated API responses.
/// Provides information about the current page, total counts, and progress.
/// </summary>
public class PaginationMetadata
{
    public int CurrentPage { get; set; }
    public int ItemsInPage { get; set; }
    public int ItemsPerPage { get; set; }
    public long? EstimatedTotal { get; set; }
    public int? EstimatedPages { get; set; }
    public bool IsExactCount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Confidence { get; set; }
    public bool HasMore { get; set; }
    
    public int StartItem => ((CurrentPage - 1) * ItemsPerPage) + 1;
    public int EndItem => StartItem + ItemsInPage - 1;
    
    public int? ProgressPercent
    {
        get
        {
            if (EstimatedTotal == null || EstimatedTotal == 0)
                return null;
            return (int)Math.Min(100, (EndItem * 100.0 / EstimatedTotal.Value));
        }
    }
}
