using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace AwsMcp.Configuration;

/// <summary>
/// Provides AWS credentials based on configuration
/// </summary>
public class AwsCredentialsProvider(AwsConfiguration config)
{
    /// <summary>
    /// Gets AWS credentials based on the configuration
    /// </summary>
    /// <returns>AWS credentials or null if using default chain</returns>
    public AWSCredentials? GetCredentials()
    {
        // If explicit credentials are provided
        if (!string.IsNullOrEmpty(config.AccessKeyId) && !string.IsNullOrEmpty(config.SecretAccessKey))
        {
            if (!string.IsNullOrEmpty(config.SessionToken))
            {
                return new SessionAWSCredentials(config.AccessKeyId, config.SecretAccessKey, config.SessionToken);
            }
            else
            {
                return new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey);
            }
        }
        
        // If profile is specified
        if (!string.IsNullOrEmpty(config.ProfileName))
        {
            var chain = new CredentialProfileStoreChain();
            if (chain.TryGetAWSCredentials(config.ProfileName, out AWSCredentials? credentials))
            {
                return credentials;
            }
            throw new InvalidOperationException($"Could not load AWS profile '{config.ProfileName}'");
        }
        
        // For LocalStack or similar, use dummy credentials
        if (!string.IsNullOrEmpty(config.ServiceUrl))
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
