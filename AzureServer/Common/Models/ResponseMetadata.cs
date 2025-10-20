namespace AzureServer.Common.Models;

/// <summary>
/// Metadata about an API response
/// </summary>
public class ResponseMetadata
{
    /// <summary>
    /// Timestamp when the response was generated
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Duration of the operation in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }
    
    /// <summary>
    /// Azure request ID for tracking
    /// </summary>
    public string? RequestId { get; set; }
    
    /// <summary>
    /// Number of items returned (for list operations)
    /// </summary>
    public int? ItemCount { get; set; }
    
    /// <summary>
    /// Total items available (if known, for pagination)
    /// </summary>
    public int? TotalItems { get; set; }
    
    /// <summary>
    /// Whether there are more results available
    /// </summary>
    public bool? HasMore { get; set; }
    
    /// <summary>
    /// Next page token for pagination
    /// </summary>
    public string? NextToken { get; set; }
}
