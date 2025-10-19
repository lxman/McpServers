using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace AwsServer.Configuration;

/// <summary>
/// Provides AWS credentials based on configuration with automatic discovery from multiple sources
/// </summary>
public class AwsCredentialsProvider(AwsConfiguration config)
{
    /// <summary>
    /// Gets AWS credentials based on the configuration with automatic discovery
    /// Discovery order:
    /// 1. Explicit credentials in config
    /// 2. AWS Profile (from credential file)
    /// 3. Environment variables (with registry fallback for when service doesn't inherit environment)
    /// 4. LocalStack dummy credentials (if ServiceUrl is set)
    /// 5. Default credential chain
    /// </summary>
    /// <returns>AWS credentials or null if using default chain</returns>
    public AWSCredentials? GetCredentials()
    {
        // If explicit credentials are provided in config
        if (!string.IsNullOrEmpty(config.AccessKeyId) && !string.IsNullOrEmpty(config.SecretAccessKey))
        {
            if (!string.IsNullOrEmpty(config.SessionToken))
            {
                return new SessionAWSCredentials(config.AccessKeyId, config.SecretAccessKey, config.SessionToken);
            }

            return new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey);
        }
        
        // If profile is specified, try AWS credential file
        if (!string.IsNullOrEmpty(config.ProfileName))
        {
            var chain = new CredentialProfileStoreChain();
            if (chain.TryGetAWSCredentials(config.ProfileName, out var credentials))
            {
                return credentials;
            }
            // Don't throw here - continue to next credential source
        }
        
        // Try to load credentials from environment variables (with registry fallback)
        // This handles the case where the service doesn't inherit system environment variables
        try
        {
            var envConfig = RegistryEnvironmentReader.GetAwsCredentialsFromEnvironment();
            if (envConfig != null && !string.IsNullOrEmpty(envConfig.AccessKeyId) 
                && !string.IsNullOrEmpty(envConfig.SecretAccessKey))
            {
                // Merge environment config with current config
                config.AccessKeyId ??= envConfig.AccessKeyId;
                config.SecretAccessKey ??= envConfig.SecretAccessKey;
                config.SessionToken ??= envConfig.SessionToken;
                
                if (config.Region == "us-east-1" && envConfig.Region != "us-east-1")
                {
                    config.Region = envConfig.Region;
                }
                
                config.ProfileName ??= envConfig.ProfileName;
                
                if (!string.IsNullOrEmpty(envConfig.SessionToken))
                {
                    return new SessionAWSCredentials(envConfig.AccessKeyId!, 
                        envConfig.SecretAccessKey!, envConfig.SessionToken);
                }

                return new BasicAWSCredentials(envConfig.AccessKeyId!, envConfig.SecretAccessKey!);
            }
        }
        catch (Exception)
        {
            // If environment/registry access fails, continue to next credential source
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
