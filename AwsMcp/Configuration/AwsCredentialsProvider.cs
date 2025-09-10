using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using AwsMcp.Configuration;

namespace AwsMcp.Configuration;

/// <summary>
/// Provides AWS credentials based on configuration
/// </summary>
public class AwsCredentialsProvider
{
    private readonly AwsConfiguration _config;
    
    public AwsCredentialsProvider(AwsConfiguration config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Gets AWS credentials based on the configuration
    /// </summary>
    /// <returns>AWS credentials or null if using default chain</returns>
    public AWSCredentials? GetCredentials()
    {
        // If explicit credentials are provided
        if (!string.IsNullOrEmpty(_config.AccessKeyId) && !string.IsNullOrEmpty(_config.SecretAccessKey))
        {
            if (!string.IsNullOrEmpty(_config.SessionToken))
            {
                return new SessionAWSCredentials(_config.AccessKeyId, _config.SecretAccessKey, _config.SessionToken);
            }
            else
            {
                return new BasicAWSCredentials(_config.AccessKeyId, _config.SecretAccessKey);
            }
        }
        
        // If profile is specified
        if (!string.IsNullOrEmpty(_config.ProfileName))
        {
            var chain = new CredentialProfileStoreChain();
            if (chain.TryGetAWSCredentials(_config.ProfileName, out AWSCredentials? credentials))
            {
                return credentials;
            }
            throw new InvalidOperationException($"Could not load AWS profile '{_config.ProfileName}'");
        }
        
        // For LocalStack or similar, use dummy credentials
        if (!string.IsNullOrEmpty(_config.ServiceUrl))
        {
            return new BasicAWSCredentials("test", "test");
        }
        
        // Fall back to default credential chain
        return null;
    }
    
    /// <summary>
    /// Gets AWS credentials using the default credential chain
    /// </summary>
    /// <returns>Default credential chain</returns>
    public static AWSCredentials GetDefaultCredentials()
    {
        // Use the environment credential provider chain
        return new EnvironmentVariablesAWSCredentials();
    }
}
