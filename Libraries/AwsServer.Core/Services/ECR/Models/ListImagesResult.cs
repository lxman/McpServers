using Amazon.ECR.Model;

namespace AwsServer.Core.Services.ECR.Models;

/// <summary>
/// Paginated response for ECR list images operation
/// </summary>
public class ListImagesResult
{
    /// <summary>
    /// Image identifiers in this page
    /// </summary>
    public List<ImageIdentifier> ImageIds { get; set; } = [];
    
    /// <summary>
    /// Number of images in this page
    /// </summary>
    public int ImageCount { get; set; }
    
    /// <summary>
    /// Whether more results are available
    /// </summary>
    public bool HasMoreResults { get; set; }
    
    /// <summary>
    /// Token to retrieve the next page of results.
    /// Pass this to the next ListImagesAsync call to continue pagination.
    /// </summary>
    public string? NextToken { get; set; }
    
    /// <summary>
    /// Repository name that was queried
    /// </summary>
    public string RepositoryName { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable summary of the results
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}