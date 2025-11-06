namespace AwsServer.Core.Common.Models;

/// <summary>
/// Pagination metadata for paginated API responses.
/// Provides information about the current page, total counts, and progress.
/// </summary>
public class PaginationMetadata
{
    /// <summary>
    /// Current page number (1-based).
    /// Calculated from pagination state or query parameters.
    /// </summary>
    public int CurrentPage { get; set; }
    
    /// <summary>
    /// Number of items in the current page.
    /// </summary>
    public int ItemsInPage { get; set; }
    
    /// <summary>
    /// Maximum items per page (the limit parameter).
    /// </summary>
    public int ItemsPerPage { get; set; }
    
    /// <summary>
    /// Estimated total number of items across all pages.
    /// Null if count cannot be estimated.
    /// </summary>
    public long? EstimatedTotal { get; set; }
    
    /// <summary>
    /// Estimated total number of pages.
    /// Null if total cannot be estimated.
    /// Calculated as: Ceiling(EstimatedTotal / ItemsPerPage)
    /// </summary>
    public int? EstimatedPages { get; set; }
    
    /// <summary>
    /// Whether the total count is exact or an estimate.
    /// True = exact count, False = estimated/approximated
    /// </summary>
    public bool IsExactCount { get; set; }
    
    /// <summary>
    /// Human-readable summary of pagination state.
    /// Examples:
    /// - "Showing results 1-100 of 473" (exact)
    /// - "Showing results 1-100 of ~500" (estimate)
    /// - "Showing results 1-100" (unknown total)
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// Confidence level of the estimate (if applicable).
    /// Examples: "Exact", "High (Insights)", "Medium (Sample-based)", "Low"
    /// Null if not applicable or unknown.
    /// </summary>
    public string? Confidence { get; set; }
    
    /// <summary>
    /// Whether there are more pages available.
    /// True if a nextToken exists or more results are known to exist.
    /// </summary>
    public bool HasMore { get; set; }
    
    /// <summary>
    /// Starting item number in this page (1-based).
    /// Example: Page 2 with 100 items per page starts at item 101.
    /// </summary>
    public int StartItem => ((CurrentPage - 1) * ItemsPerPage) + 1;
    
    /// <summary>
    /// Ending item number in this page (1-based).
    /// Example: Page 1 with 100 items has EndItem = 100.
    /// </summary>
    public int EndItem => StartItem + ItemsInPage - 1;
    
    /// <summary>
    /// Progress percentage through the total results (0-100).
    /// Null if total count is unknown.
    /// </summary>
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
