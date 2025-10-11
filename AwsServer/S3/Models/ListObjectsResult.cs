namespace AwsServer.S3.Models;

/// <summary>
/// Paginated response for S3 list objects operation
/// </summary>
public class ListObjectsResult
{
    /// <summary>
    /// S3 objects in this page
    /// </summary>
    public List<Amazon.S3.Model.S3Object> Objects { get; set; } = [];
    
    /// <summary>
    /// Number of objects in this page
    /// </summary>
    public int ObjectCount { get; set; }
    
    /// <summary>
    /// Whether more results are available
    /// </summary>
    public bool HasMoreResults { get; set; }
    
    /// <summary>
    /// Continuation token to retrieve the next page of results.
    /// Pass this to the next ListObjectsAsync call to continue pagination.
    /// </summary>
    public string? ContinuationToken { get; set; }
    
    /// <summary>
    /// Bucket name that was queried
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// Prefix filter used (if any)
    /// </summary>
    public string? Prefix { get; set; }
    
    /// <summary>
    /// Human-readable summary of the results
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}