using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using AwsMcp.Configuration.Models;
using Microsoft.Extensions.Logging;

namespace AwsMcp.Configuration;

/// <summary>
/// Service for AWS account discovery, configuration detection, and permission testing
/// </summary>
public class AwsDiscoveryService
{
    private readonly ILogger<AwsDiscoveryService> _logger;
    private AmazonSecurityTokenServiceClient? _stsClient;
    private AwsConfiguration? _config;
    private AWSCredentials? _credentials;

    public AwsDiscoveryService(ILogger<AwsDiscoveryService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Auto-discover and initialize with working credentials
    /// </summary>
    public async Task<bool> AutoInitializeAsync()
    {
        try
        {
            // Try default credential chain first
            var config = new AwsConfiguration
            {
                Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? 
                         Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? 
                         "us-east-1"
            };
        
            // Try to detect region from CLI config
            var cliConfig = DetectCliConfiguration();
            var defaultProfile = cliConfig.Profiles.FirstOrDefault(p => p.Name == "default");
            if (defaultProfile?.Region != null)
            {
                config.Region = defaultProfile.Region;
            }
        
            // Try profile-based credentials first
            var profileName = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default";
            var chain = new CredentialProfileStoreChain();
            if (chain.TryGetAWSCredentials(profileName, out var profileCredentials))
            {
                config.ProfileName = profileName;
                return await InitializeAsync(config);
            }
        
            // Fall back to environment variables / instance profile
            return await InitializeAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-initialize AWS Discovery service");
            return false;
        }
    }

    /// <summary>
    /// Initialize the discovery service with AWS configuration
    /// </summary>
    public async Task<bool> InitializeAsync(AwsConfiguration config)
    {
        try
        {
            _config = config;

            var clientConfig = new AmazonSecurityTokenServiceConfig
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps
            };

            // Set custom endpoint if provided (for LocalStack, etc.)
            if (!string.IsNullOrEmpty(config.ServiceUrl))
            {
                clientConfig.ServiceURL = config.ServiceUrl;
            }

            var credentialsProvider = new AwsCredentialsProvider(config);
            _credentials = credentialsProvider.GetCredentials();

            if (_credentials != null)
            {
                _stsClient = new AmazonSecurityTokenServiceClient(_credentials, clientConfig);
            }
            else
            {
                _stsClient = new AmazonSecurityTokenServiceClient(clientConfig);
            }

            _logger.LogInformation("AWS Discovery service initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AWS Discovery service");
            return false;
        }
    }

    /// <summary>
    /// Get AWS account information and caller identity using STS
    /// </summary>
    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        EnsureInitialized();

        try
        {
            var response = await _stsClient!.GetCallerIdentityAsync(new GetCallerIdentityRequest());

            // Determine if this is GovCloud based on the ARN
            var isGovCloud = response.Arn.Contains("aws-us-gov") || response.Arn.Contains("-gov-") || response.Arn.Contains(".amazonaws-us-gov.com");

            // Infer region from ARN
            var inferredRegion = InferRegionFromArn(response.Arn);

            return new AccountInfo
            {
                AccountId = response.Account,
                UserId = response.UserId,
                Arn = response.Arn,
                PrincipalName = ExtractPrincipalName(response.Arn),
                IsGovCloud = isGovCloud,
                InferredRegion = inferredRegion,
                ConfiguredRegion = _config?.Region ?? "us-east-1"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AWS account information");
            throw;
        }
    }

    /// <summary>
    /// Automatically discover optimal AWS configuration settings
    /// </summary>
    public async Task<AutoDiscoveryResult> AutoDiscoverConfigurationAsync()
    {
        try
        {
            var result = new AutoDiscoveryResult();

            // Step 1: Detect existing configuration
            result.CliConfiguration = DetectCliConfiguration();
            result.EnvironmentVariables = DetectEnvironmentVariables();

            // Step 2: Try to get account info with current configuration
            try
            {
                result.AccountInfo = await GetAccountInfoAsync();
                result.AuthenticationStatus = "Success";
            }
            catch (Exception ex)
            {
                result.AuthenticationStatus = "Failed";
                result.AuthenticationError = ex.Message;
            }

            // Step 3: Test service permissions if authentication succeeded
            if (result.AccountInfo != null)
            {
                result.ServicePermissions = await TestServicePermissionsAsync(result.AccountInfo.InferredRegion);
                result.RecommendedConfiguration = GenerateRecommendedConfiguration(result);
            }

            // Step 4: Generate troubleshooting suggestions
            result.TroubleshootingSuggestions = GenerateTroubleshootingSuggestions(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-discover configuration");
            throw;
        }
    }

    /// <summary>
    /// Test what AWS service permissions are available
    /// </summary>
    public async Task<List<ServicePermissionTest>> TestServicePermissionsAsync(string region = "us-east-1")
    {
        var results = new List<ServicePermissionTest>();

        // Test STS (should work if we got this far)
        results.Add(await TestStsPermissions());

        // Test CloudWatch Metrics
        results.Add(await TestCloudWatchMetricsPermissions(region));

        // Test CloudWatch Logs
        results.Add(await TestCloudWatchLogsPermissions(region));

        // Test S3
        results.Add(await TestS3Permissions(region));

        // Test ECR
        results.Add(await TestEcrPermissions(region));

        // Test ECS
        results.Add(await TestEcsPermissions(region));

        return results;
    }

    /// <summary>
    /// Detect AWS CLI configuration and available profiles
    /// </summary>
    public CliConfiguration DetectCliConfiguration()
    {
        var config = new CliConfiguration();

        try
        {
            // Check for AWS CLI config files
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var awsDir = Path.Combine(homeDir, ".aws");

            var configFile = Path.Combine(awsDir, "config");
            var credentialsFile = Path.Combine(awsDir, "credentials");

            config.ConfigFileExists = File.Exists(configFile);
            config.CredentialsFileExists = File.Exists(credentialsFile);

            if (config.ConfigFileExists)
            {
                config.ConfigFilePath = configFile;
                config.Profiles.AddRange(ParseAwsConfigFile(configFile));
            }

            if (config.CredentialsFileExists)
            {
                config.CredentialsFilePath = credentialsFile;
                var credProfiles = ParseAwsCredentialsFile(credentialsFile);
                foreach (var profile in credProfiles)
                {
                    if (!config.Profiles.Any(p => p.Name == profile.Name))
                    {
                        config.Profiles.Add(profile);
                    }
                }
            }

            // Detect current profile
            config.CurrentProfile = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default";

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting CLI configuration");
            config.DetectionError = ex.Message;
        }

        return config;
    }

    #region Private Helper Methods

    private void EnsureInitialized()
    {
        if (_stsClient == null)
        {
            throw new InvalidOperationException(
                "AWS Discovery service is not initialized. Call InitializeAsync first.");
        }
    }

    private string InferRegionFromArn(string arn)
    {
        try
        {
            // ARN format: arn:partition:service:region:account-id:resource
            var parts = arn.Split(':');
            if (parts.Length >= 4 && !string.IsNullOrEmpty(parts[3]))
            {
                return parts[3]; // Region is the 4th part (0-indexed)
            }
        
            // For global services like IAM, fall back to configured region
            if (parts.Length >= 2 && parts[2] == "iam")
            {
                // Use CLI config region as fallback
                var cliConfig = DetectCliConfiguration();
                var defaultProfile = cliConfig.Profiles.FirstOrDefault(p => p.Name == "default");
                if (defaultProfile?.Region != null)
                {
                    return defaultProfile.Region;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to infer region from ARN: {Arn}", arn);
        }

        return _config?.Region ?? "us-east-1";
    }

    private string ExtractPrincipalName(string arn)
    {
        try
        {
            // Extract user or role name from ARN
            if (arn.Contains(":user/"))
            {
                return arn.Split(":user/")[1];
            }

            if (arn.Contains(":role/"))
            {
                return arn.Split(":role/")[1];
            }

            if (arn.Contains(":assumed-role/"))
            {
                var parts = arn.Split(":assumed-role/")[1].Split('/');
                return parts.Length > 1 ? $"{parts[0]} (assumed by {parts[1]})" : parts[0];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract principal name from ARN: {Arn}", arn);
        }

        return "Unknown";
    }

    private static EnvironmentVariableInfo DetectEnvironmentVariables()
    {
        return new EnvironmentVariableInfo
        {
            AwsAccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
            AwsSecretAccessKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"))
                ? "***PRESENT***"
                : null,
            AwsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ??
                        Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION"),
            AwsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE"),
            AwsRoleArn = Environment.GetEnvironmentVariable("AWS_ROLE_ARN"),
            AwsSessionToken = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN"))
                ? "***PRESENT***"
                : null
        };
    }

    private async Task<ServicePermissionTest> TestStsPermissions()
    {
        try
        {
            await _stsClient!.GetCallerIdentityAsync(new GetCallerIdentityRequest());
            return new ServicePermissionTest
            {
                ServiceName = "STS",
                HasPermission = true,
                TestedOperation = "GetCallerIdentity",
                Status = "Success"
            };
        }
        catch (Exception ex)
        {
            return new ServicePermissionTest
            {
                ServiceName = "STS",
                HasPermission = false,
                TestedOperation = "GetCallerIdentity",
                Status = "Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ServicePermissionTest> TestCloudWatchMetricsPermissions(string region)
    {
        try
        {
            var config = new Amazon.CloudWatch.AmazonCloudWatchConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            };

            Amazon.CloudWatch.AmazonCloudWatchClient client;
            if (_credentials != null)
            {
                client = new Amazon.CloudWatch.AmazonCloudWatchClient(_credentials, config);
            }
            else
            {
                client = new Amazon.CloudWatch.AmazonCloudWatchClient(config);
            }

            using (client)
            {
                await client.ListMetricsAsync(new Amazon.CloudWatch.Model.ListMetricsRequest());
            }

            return new ServicePermissionTest
            {
                ServiceName = "CloudWatch Metrics",
                HasPermission = true,
                TestedOperation = "ListMetrics",
                Status = "Success"
            };
        }
        catch (Exception ex)
        {
            return new ServicePermissionTest
            {
                ServiceName = "CloudWatch Metrics",
                HasPermission = false,
                TestedOperation = "ListMetrics",
                Status = "Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ServicePermissionTest> TestCloudWatchLogsPermissions(string region)
    {
        try
        {
            var config = new Amazon.CloudWatchLogs.AmazonCloudWatchLogsConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            };

            Amazon.CloudWatchLogs.AmazonCloudWatchLogsClient client;
            if (_credentials != null)
            {
                client = new Amazon.CloudWatchLogs.AmazonCloudWatchLogsClient(_credentials, config);
            }
            else
            {
                client = new Amazon.CloudWatchLogs.AmazonCloudWatchLogsClient(config);
            }

            using (client)
            {
                await client.DescribeLogGroupsAsync(new Amazon.CloudWatchLogs.Model.DescribeLogGroupsRequest
                    { Limit = 1 });
            }

            return new ServicePermissionTest
            {
                ServiceName = "CloudWatch Logs",
                HasPermission = true,
                TestedOperation = "DescribeLogGroups",
                Status = "Success"
            };
        }
        catch (Exception ex)
        {
            return new ServicePermissionTest
            {
                ServiceName = "CloudWatch Logs",
                HasPermission = false,
                TestedOperation = "DescribeLogGroups",
                Status = "Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ServicePermissionTest> TestS3Permissions(string region)
    {
        try
        {
            var config = new Amazon.S3.AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            };

            Amazon.S3.AmazonS3Client client;
            if (_credentials != null)
            {
                client = new Amazon.S3.AmazonS3Client(_credentials, config);
            }
            else
            {
                client = new Amazon.S3.AmazonS3Client(config);
            }

            using (client)
            {
                await client.ListBucketsAsync();
            }

            return new ServicePermissionTest
            {
                ServiceName = "S3",
                HasPermission = true,
                TestedOperation = "ListBuckets",
                Status = "Success"
            };
        }
        catch (Exception ex)
        {
            return new ServicePermissionTest
            {
                ServiceName = "S3",
                HasPermission = false,
                TestedOperation = "ListBuckets",
                Status = "Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ServicePermissionTest> TestEcrPermissions(string region)
    {
        try
        {
            var config = new Amazon.ECR.AmazonECRConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            };

            Amazon.ECR.AmazonECRClient client;
            if (_credentials != null)
            {
                client = new Amazon.ECR.AmazonECRClient(_credentials, config);
            }
            else
            {
                client = new Amazon.ECR.AmazonECRClient(config);
            }

            using (client)
            {
                await client.DescribeRepositoriesAsync(new Amazon.ECR.Model.DescribeRepositoriesRequest
                    { MaxResults = 1 });
            }

            return new ServicePermissionTest
            {
                ServiceName = "ECR",
                HasPermission = true,
                TestedOperation = "DescribeRepositories",
                Status = "Success"
            };
        }
        catch (Exception ex)
        {
            return new ServicePermissionTest
            {
                ServiceName = "ECR",
                HasPermission = false,
                TestedOperation = "DescribeRepositories",
                Status = "Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ServicePermissionTest> TestEcsPermissions(string region)
    {
        try
        {
            var config = new Amazon.ECS.AmazonECSConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            };

            Amazon.ECS.AmazonECSClient client;
            if (_credentials != null)
            {
                client = new Amazon.ECS.AmazonECSClient(_credentials, config);
            }
            else
            {
                client = new Amazon.ECS.AmazonECSClient(config);
            }

            using (client)
            {
                await client.ListClustersAsync(new Amazon.ECS.Model.ListClustersRequest { MaxResults = 1 });
            }

            return new ServicePermissionTest
            {
                ServiceName = "ECS",
                HasPermission = true,
                TestedOperation = "ListClusters",
                Status = "Success"
            };
        }
        catch (Exception ex)
        {
            return new ServicePermissionTest
            {
                ServiceName = "ECS",
                HasPermission = false,
                TestedOperation = "ListClusters",
                Status = "Failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private List<AwsProfile> ParseAwsConfigFile(string configFile)
    {
        var profiles = new List<AwsProfile>();

        try
        {
            var lines = File.ReadAllLines(configFile);
            AwsProfile? currentProfile = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    // New profile section
                    var profileName = trimmed.Trim('[', ']');
                    if (profileName.StartsWith("profile "))
                        profileName = profileName.Substring(8);

                    currentProfile = new AwsProfile { Name = profileName };
                    profiles.Add(currentProfile);
                }
                else if (currentProfile != null && trimmed.Contains("="))
                {
                    var parts = trimmed.Split('=', 2);
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    switch (key.ToLower())
                    {
                        case "region":
                            currentProfile.Region = value;
                            break;
                        case "output":
                            currentProfile.Output = value;
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing AWS config file: {ConfigFile}", configFile);
        }

        return profiles;
    }

    private List<AwsProfile> ParseAwsCredentialsFile(string credentialsFile)
    {
        var profiles = new List<AwsProfile>();
    
        try
        {
            // Use AWS SDK's built-in profile detection instead of manual parsing
            var chain = new CredentialProfileStoreChain();
            var credentialProfiles = chain.ListProfiles();
        
            foreach (var credentialProfile in credentialProfiles)
            {
                var profile = new AwsProfile { Name = credentialProfile.Name };
            
                // Actually test if credentials work
                if (chain.TryGetAWSCredentials(credentialProfile.Name, out var credentials))
                {
                    profile.HasAccessKey = true;
                    profile.HasSecretKey = true;
                
                    // Try to get region from the profile
                    if (chain.TryGetProfile(credentialProfile.Name, out var profileInfo))
                    {
                        profile.Region = profileInfo.Region?.SystemName ?? "us-east-1";
                    }
                }
            
                profiles.Add(profile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing AWS credentials file: {CredentialsFile}", credentialsFile);
        }
    
        return profiles;
    }

    private static RecommendedConfiguration GenerateRecommendedConfiguration(AutoDiscoveryResult result)
    {
        var config = new RecommendedConfiguration();

        // Recommend region based on inferred region from account info
        if (result.AccountInfo != null)
        {
            config.RecommendedRegion = result.AccountInfo.InferredRegion;
            config.Reasoning.Add($"Using region {result.AccountInfo.InferredRegion} inferred from your AWS identity");
        }

        // Analyze service permissions and recommend initialization strategies
        if (result.ServicePermissions != null)
        {
            var workingServices = result.ServicePermissions.Where(s => s.HasPermission).ToList();
            var failedServices = result.ServicePermissions.Where(s => !s.HasPermission).ToList();

            if (workingServices.Count != 0)
            {
                config.InitializationStrategy = "Partial service initialization";
                config.WorkingServices = workingServices.Select(s => s.ServiceName).ToList();
                config.Reasoning.Add(
                    $"You have permissions for {workingServices.Count} out of {result.ServicePermissions.Count} services");
            }

            if (failedServices.Count != 0)
            {
                config.FailedServices = failedServices.Select(s => s.ServiceName).ToList();
                config.Reasoning.Add($"Services without permissions: {string.Join(", ", config.FailedServices)}");
            }
        }

        return config;
    }

    private static List<string> GenerateTroubleshootingSuggestions(AutoDiscoveryResult result)
    {
        var suggestions = new List<string>();

        if (result.AuthenticationStatus == "Failed")
        {
            suggestions.Add("AWS authentication failed - check your credentials");

            if (result.EnvironmentVariables?.AwsAccessKeyId == null &&
                (result.CliConfiguration?.Profiles?.Count ?? 0) == 0)
            {
                suggestions.Add("No AWS credentials found - configure AWS CLI or set environment variables");
                suggestions.Add("Run 'aws configure' to set up credentials");
            }

            if (!string.IsNullOrEmpty(result.EnvironmentVariables?.AwsAccessKeyId))
            {
                suggestions.Add("Environment variables found - verify AWS_SECRET_ACCESS_KEY is set correctly");
            }

            if (result.CliConfiguration?.ConfigFileExists == false)
            {
                suggestions.Add("AWS CLI config not found - run 'aws configure' to create configuration");
            }
        }

        if (result.ServicePermissions != null)
        {
            var failedServices = result.ServicePermissions.Where(s => !s.HasPermission).ToList();
            if (failedServices.Count != 0)
            {
                suggestions.Add(
                    $"Missing permissions for {failedServices.Count} services - contact your AWS administrator");
                suggestions.Add(
                    "Use service-specific initialization (testMetricsOnly/testLogsOnly) for available services");
            }
        }

        return suggestions;
    }

    #endregion

    public void Dispose()
    {
        _stsClient?.Dispose();
    }
}