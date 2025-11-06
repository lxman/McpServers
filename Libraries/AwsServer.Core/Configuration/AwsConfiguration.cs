using Amazon;

namespace AwsServer.Core.Configuration;

/// <summary>
/// Configuration settings for AWS services
/// </summary>
public class AwsConfiguration
{
    /// <summary>
    /// AWS Access Key ID
    /// </summary>
    public string? AccessKeyId { get; set; }
    
    /// <summary>
    /// AWS Secret Access Key
    /// </summary>
    public string? SecretAccessKey { get; set; }
    
    /// <summary>
    /// AWS Session Token (for temporary credentials)
    /// </summary>
    public string? SessionToken { get; set; }
    
    /// <summary>
    /// AWS Region endpoint
    /// </summary>
    public string Region { get; set; } = "us-east-1";
    
    /// <summary>
    /// AWS Profile name (for credential file)
    /// </summary>
    public string? ProfileName { get; set; }
    
    /// <summary>
    /// Custom endpoint URL (for local testing like LocalStack)
    /// </summary>
    public string? ServiceUrl { get; set; }
    
    /// <summary>
    /// Whether to force path style for S3 (useful for LocalStack)
    /// </summary>
    public bool ForcePathStyle { get; set; } = false;
    
    /// <summary>
    /// Whether to use HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = true;
    
    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Converts the region string to Amazon RegionEndpoint
    /// </summary>
    public RegionEndpoint GetRegionEndpoint()
    {
        return RegionEndpoint.GetBySystemName(Region);
    }
    
    /// <summary>
    /// Validates the configuration
    /// </summary>
    public bool IsValid()
    {
        // If using profile, we don't need explicit credentials
        if (!string.IsNullOrEmpty(ProfileName))
            return true;
            
        // If using service URL (LocalStack), credentials can be dummy
        if (!string.IsNullOrEmpty(ServiceUrl))
            return true;
            
        // Otherwise, we need access key and secret
        return !string.IsNullOrEmpty(AccessKeyId) && !string.IsNullOrEmpty(SecretAccessKey);
    }
}
