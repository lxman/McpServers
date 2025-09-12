using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using AwsMcp.Configuration;
using AwsMcp.Configuration.Models;

namespace AwsMcp.Tools;

[McpServerToolType]
public class AwsDiscoveryTools
{
    private readonly AwsDiscoveryService _discoveryService;

    public AwsDiscoveryTools(AwsDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    [McpServerTool]
    [Description("Initialize AWS Discovery service with AWS credentials and configuration")]
    public async Task<string> InitializeAwsDiscovery(
        [Description("AWS region (default: us-east-1)")]
        string region = "us-east-1",
        [Description("AWS Access Key ID (optional if using profile or environment)")]
        string? accessKeyId = null,
        [Description("AWS Secret Access Key (optional if using profile or environment)")]
        string? secretAccessKey = null,
        [Description("AWS Profile name (optional)")]
        string? profileName = null,
        [Description("Custom service URL for LocalStack or other endpoints (optional)")]
        string? serviceUrl = null)
    {
        try
        {
            var config = new AwsConfiguration
            {
                Region = region,
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                ProfileName = profileName,
                ServiceUrl = serviceUrl
            };

            bool success = await _discoveryService.InitializeAsync(config);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "AWS Discovery service initialized successfully" : "Failed to initialize AWS Discovery service",
                region,
                usingProfile = !string.IsNullOrEmpty(profileName),
                usingCustomEndpoint = !string.IsNullOrEmpty(serviceUrl)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "initializing AWS Discovery service");
        }
    }

    [McpServerTool]
    [Description("Get current AWS account information and caller identity using STS")]
    public async Task<string> GetAccountInfo()
    {
        try
        {
            var accountInfo = await _discoveryService.GetAccountInfoAsync();
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                accountInfo = new
                {
                    accountId = accountInfo.AccountId,
                    userId = accountInfo.UserId,
                    arn = accountInfo.Arn,
                    principalName = accountInfo.PrincipalName,
                    isGovCloud = accountInfo.IsGovCloud,
                    inferredRegion = accountInfo.InferredRegion,
                    configuredRegion = accountInfo.ConfiguredRegion
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "getting AWS account information");
        }
    }

    [McpServerTool]
    [Description("Automatically discover optimal AWS configuration settings")]
    public async Task<string> AutoDiscoverConfiguration()
    {
        try
        {
            var result = await _discoveryService.AutoDiscoverConfigurationAsync();
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                authenticationStatus = result.AuthenticationStatus,
                authenticationError = result.AuthenticationError,
                accountInfo = result.AccountInfo != null ? new
                {
                    accountId = result.AccountInfo.AccountId,
                    principalName = result.AccountInfo.PrincipalName,
                    isGovCloud = result.AccountInfo.IsGovCloud,
                    inferredRegion = result.AccountInfo.InferredRegion
                } : null,
                cliConfiguration = new
                {
                    configFileExists = result.CliConfiguration.ConfigFileExists,
                    credentialsFileExists = result.CliConfiguration.CredentialsFileExists,
                    profileCount = result.CliConfiguration.Profiles.Count,
                    currentProfile = result.CliConfiguration.CurrentProfile,
                    profiles = result.CliConfiguration.Profiles.Select(p => new
                    {
                        name = p.Name,
                        region = p.Region,
                        hasCredentials = p is { HasAccessKey: true, HasSecretKey: true }
                    }).ToList()
                },
                environmentVariables = new
                {
                    hasAwsAccessKeyId = !string.IsNullOrEmpty(result.EnvironmentVariables.AwsAccessKeyId),
                    hasAwsSecretAccessKey = !string.IsNullOrEmpty(result.EnvironmentVariables.AwsSecretAccessKey),
                    awsRegion = result.EnvironmentVariables.AwsRegion,
                    awsProfile = result.EnvironmentVariables.AwsProfile,
                    hasAwsSessionToken = !string.IsNullOrEmpty(result.EnvironmentVariables.AwsSessionToken)
                },
                servicePermissions = result.ServicePermissions?.Select(sp => new
                {
                    serviceName = sp.ServiceName,
                    hasPermission = sp.HasPermission,
                    testedOperation = sp.TestedOperation,
                    status = sp.Status,
                    errorMessage = sp.ErrorMessage
                }).ToList(),
                recommendedConfiguration = result.RecommendedConfiguration != null ? new
                {
                    recommendedRegion = result.RecommendedConfiguration.RecommendedRegion,
                    initializationStrategy = result.RecommendedConfiguration.InitializationStrategy,
                    workingServices = result.RecommendedConfiguration.WorkingServices,
                    failedServices = result.RecommendedConfiguration.FailedServices,
                    reasoning = result.RecommendedConfiguration.Reasoning
                } : null,
                troubleshootingSuggestions = result.TroubleshootingSuggestions
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "auto-discovering AWS configuration");
        }
    }

    [McpServerTool]
    [Description("Test what AWS service permissions are available")]
    public async Task<string> TestServicePermissions(
        [Description("AWS region to test permissions in (default: us-east-1)")]
        string region = "us-east-1")
    {
        try
        {
            var results = await _discoveryService.TestServicePermissionsAsync(region);
            
            var workingServices = results.Where(r => r.HasPermission).ToList();
            var failedServices = results.Where(r => !r.HasPermission).ToList();
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                region,
                summary = new
                {
                    totalServicesTested = results.Count,
                    workingServices = workingServices.Count,
                    failedServices = failedServices.Count,
                    successRate = $"{(workingServices.Count / (double)results.Count) * 100:F1}%"
                },
                workingServices = workingServices.Select(s => new
                {
                    serviceName = s.ServiceName,
                    testedOperation = s.TestedOperation,
                    status = s.Status
                }).ToList(),
                failedServices = failedServices.Select(s => new
                {
                    serviceName = s.ServiceName,
                    testedOperation = s.TestedOperation,
                    status = s.Status,
                    errorMessage = s.ErrorMessage
                }).ToList(),
                detailedResults = results.Select(r => new
                {
                    serviceName = r.ServiceName,
                    hasPermission = r.HasPermission,
                    testedOperation = r.TestedOperation,
                    status = r.Status,
                    errorMessage = r.ErrorMessage
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "testing AWS service permissions");
        }
    }

    [McpServerTool]
    [Description("Detect AWS CLI configuration and available profiles")]
    public async Task<string> DetectCliConfiguration()
    {
        try
        {
            var cliConfig = _discoveryService.DetectCliConfiguration();
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                configFileExists = cliConfig.ConfigFileExists,
                credentialsFileExists = cliConfig.CredentialsFileExists,
                configFilePath = cliConfig.ConfigFilePath,
                credentialsFilePath = cliConfig.CredentialsFilePath,
                currentProfile = cliConfig.CurrentProfile,
                profileCount = cliConfig.Profiles.Count,
                detectionError = cliConfig.DetectionError,
                profiles = cliConfig.Profiles.Select(p => new
                {
                    name = p.Name,
                    region = p.Region,
                    output = p.Output,
                    hasAccessKey = p.HasAccessKey,
                    hasSecretKey = p.HasSecretKey,
                    isComplete = p is { HasAccessKey: true, HasSecretKey: true }
                }).ToList(),
                recommendations = GenerateCliRecommendations(cliConfig)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "detecting AWS CLI configuration");
        }
    }

    [McpServerTool]
    [Description("Get comprehensive AWS environment analysis and setup recommendations")]
    public async Task<string> GetEnvironmentAnalysis()
    {
        try
        {
            // Run full discovery
            var discoveryResult = await _discoveryService.AutoDiscoverConfigurationAsync();
            
            // Analyze the current state
            var analysis = new
            {
                success = true,
                analysisTimestamp = DateTime.UtcNow,
                
                // Authentication status
                authentication = new
                {
                    status = discoveryResult.AuthenticationStatus,
                    error = discoveryResult.AuthenticationError,
                    isWorking = discoveryResult.AuthenticationStatus == "Success"
                },
                
                // Account information
                account = discoveryResult.AccountInfo != null ? new
                {
                    id = discoveryResult.AccountInfo.AccountId,
                    principal = discoveryResult.AccountInfo.PrincipalName,
                    arn = discoveryResult.AccountInfo.Arn,
                    region = discoveryResult.AccountInfo.InferredRegion,
                    isGovCloud = discoveryResult.AccountInfo.IsGovCloud
                } : null,
                
                // Configuration sources
                configurationSources = new
                {
                    environmentVariables = new
                    {
                        present = !string.IsNullOrEmpty(discoveryResult.EnvironmentVariables.AwsAccessKeyId),
                        profile = discoveryResult.EnvironmentVariables.AwsProfile,
                        region = discoveryResult.EnvironmentVariables.AwsRegion
                    },
                    awsCli = new
                    {
                        configExists = discoveryResult.CliConfiguration.ConfigFileExists,
                        credentialsExist = discoveryResult.CliConfiguration.CredentialsFileExists,
                        profileCount = discoveryResult.CliConfiguration.Profiles.Count,
                        currentProfile = discoveryResult.CliConfiguration.CurrentProfile
                    }
                },
                
                // Service permissions summary
                services = discoveryResult.ServicePermissions != null ? new
                {
                    total = discoveryResult.ServicePermissions.Count,
                    working = discoveryResult.ServicePermissions.Count(s => s.HasPermission),
                    failed = discoveryResult.ServicePermissions.Count(s => !s.HasPermission),
                    details = discoveryResult.ServicePermissions.ToDictionary(
                        s => s.ServiceName.Replace(" ", "").ToLower(),
                        s => s.HasPermission
                    )
                } : null,
                
                // Recommendations
                recommendations = new
                {
                    region = discoveryResult.RecommendedConfiguration?.RecommendedRegion,
                    strategy = discoveryResult.RecommendedConfiguration?.InitializationStrategy,
                    workingServices = discoveryResult.RecommendedConfiguration?.WorkingServices ?? new List<string>(),
                    troubleshooting = discoveryResult.TroubleshootingSuggestions
                },
                
                // Ready-to-use initialization commands
                suggestedCommands = GenerateInitializationCommands(discoveryResult)
            };
            
            return JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "analyzing AWS environment");
        }
    }

    #region Private Helper Methods

    private static List<string> GenerateCliRecommendations(CliConfiguration cliConfig)
    {
        var recommendations = new List<string>();
        
        if (cliConfig is { ConfigFileExists: false, CredentialsFileExists: false })
        {
            recommendations.Add("No AWS CLI configuration found - run 'aws configure' to set up credentials");
            recommendations.Add("Alternative: Set environment variables AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY");
        }
        else if (cliConfig.Profiles.Count == 0)
        {
            recommendations.Add("AWS CLI files exist but no profiles found - check file format");
        }
        else
        {
            var incompleteProfiles = cliConfig.Profiles.Where(p => !p.HasAccessKey || !p.HasSecretKey).ToList();
            if (incompleteProfiles.Any())
            {
                recommendations.Add($"Incomplete profiles found: {string.Join(", ", incompleteProfiles.Select(p => p.Name))}");
                recommendations.Add("Complete profiles need both aws_access_key_id and aws_secret_access_key");
            }
            
            var completeProfiles = cliConfig.Profiles.Where(p => p is { HasAccessKey: true, HasSecretKey: true }).ToList();
            if (completeProfiles.Any())
            {
                recommendations.Add($"Complete profiles available: {string.Join(", ", completeProfiles.Select(p => p.Name))}");
            }
        }
        
        return recommendations;
    }

    private static Dictionary<string, object> GenerateInitializationCommands(AutoDiscoveryResult result)
    {
        var commands = new Dictionary<string, object>();
        
        if (result is { AccountInfo: not null, ServicePermissions: not null })
        {
            // Generate CloudWatch initialization
            var cwMetrics = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "CloudWatch Metrics");
            var cwLogs = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "CloudWatch Logs");
            
            if (cwMetrics?.HasPermission == true && cwLogs?.HasPermission == true)
            {
                commands["cloudwatch_full"] = new
                {
                    tool = "InitializeCloudWatch",
                    parameters = new { region = result.AccountInfo.InferredRegion },
                    description = "Full CloudWatch initialization (metrics and logs)"
                };
            }
            else if (cwMetrics?.HasPermission == true)
            {
                commands["cloudwatch_metrics"] = new
                {
                    tool = "InitializeCloudWatch",
                    parameters = new 
                    { 
                        region = result.AccountInfo.InferredRegion,
                        testMetricsOnly = true
                    },
                    description = "CloudWatch metrics only initialization"
                };
            }
            else if (cwLogs?.HasPermission == true)
            {
                commands["cloudwatch_logs"] = new
                {
                    tool = "InitializeCloudWatch", 
                    parameters = new
                    {
                        region = result.AccountInfo.InferredRegion,
                        testLogsOnly = true
                    },
                    description = "CloudWatch logs only initialization"
                };
            }
            
            // Add other service initialization commands based on permissions
            var s3Permission = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "S3");
            if (s3Permission?.HasPermission == true)
            {
                commands["s3"] = new
                {
                    tool = "InitializeS3",
                    parameters = new { region = result.AccountInfo.InferredRegion },
                    description = "S3 service initialization"
                };
            }
            
            var ecrPermission = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "ECR");
            if (ecrPermission?.HasPermission == true)
            {
                commands["ecr"] = new
                {
                    tool = "InitializeECR",
                    parameters = new { region = result.AccountInfo.InferredRegion },
                    description = "ECR service initialization"
                };
            }
            
            var ecsPermission = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "ECS");
            if (ecsPermission?.HasPermission == true)
            {
                commands["ecs"] = new
                {
                    tool = "InitializeECS",
                    parameters = new { region = result.AccountInfo.InferredRegion },
                    description = "ECS service initialization"
                };
            }
        }
        
        return commands;
    }

    /// <summary>
    /// Enhanced error handling for AWS Discovery operations with user-friendly messages
    /// </summary>
    private static string HandleError(Exception ex, string operation)
    {
        object error;

        if (ex is InvalidOperationException invalidOpEx)
        {
            error = new
            {
                success = false,
                error = "AWS Discovery service not initialized or missing permissions",
                details = invalidOpEx.Message,
                suggestedActions = new[]
                {
                    "Ensure you have called InitializeAwsDiscovery first",
                    "Check your AWS credentials and permissions",
                    "Verify your AWS region is correct",
                    "For discovery operations, you need at least STS:GetCallerIdentity permission"
                },
                errorType = "ServiceNotInitialized"
            };
        }
        else if (ex is Amazon.Runtime.AmazonServiceException awsEx)
        {
            var actions = awsEx.ErrorCode switch
            {
                "AccessDenied" => new[]
                {
                    "Check your IAM permissions for AWS STS",
                    "Ensure your user/role has STS:GetCallerIdentity permission",
                    "Try: aws sts get-caller-identity to verify your credentials from CLI"
                },
                "UnauthorizedOperation" => new[]
                {
                    "Your AWS credentials don't have permission for STS operations",
                    "Contact your AWS administrator to grant STS permissions",
                    "STS:GetCallerIdentity is the minimum required permission"
                },
                "InvalidUserID.NotFound" => new[]
                {
                    "The AWS access key ID provided does not exist",
                    "Check your access key ID for typos",
                    "Verify the access key hasn't been deleted or deactivated"
                },
                "SignatureDoesNotMatch" => new[]
                {
                    "The AWS secret access key provided is incorrect",
                    "Check your secret access key for typos",
                    "Ensure there are no extra spaces in your credentials"
                },
                "TokenRefreshRequired" => new[]
                {
                    "Your AWS session token has expired",
                    "Refresh your credentials or remove the session token",
                    "If using temporary credentials, obtain new ones"
                },
                _ => new[]
                {
                    "Check AWS STS service status at https://status.aws.amazon.com/",
                    "Verify your credentials and try again",
                    $"AWS Error Code: {awsEx.ErrorCode} - consult AWS documentation"
                }
            };

            error = new
            {
                success = false,
                error = $"AWS STS service error: {awsEx.ErrorCode}",
                details = awsEx.Message,
                suggestedActions = actions,
                errorType = "AWSService",
                statusCode = awsEx.StatusCode.ToString(),
                awsErrorCode = awsEx.ErrorCode
            };
        }
        else if (ex is Amazon.Runtime.AmazonClientException clientEx)
        {
            error = new
            {
                success = false,
                error = "AWS client error - network or configuration issue",
                details = clientEx.Message,
                suggestedActions = new[]
                {
                    "Check your internet connection",
                    "Verify AWS endpoint configuration",
                    "Check if you're behind a firewall or proxy",
                    "Try with a different AWS region",
                    "Verify your AWS service URL if using LocalStack"
                },
                errorType = "NetworkOrConfiguration"
            };
        }
        else if (ex is ArgumentException argEx)
        {
            error = new
            {
                success = false,
                error = "Invalid parameter provided to AWS Discovery operation",
                details = argEx.Message,
                suggestedActions = new[]
                {
                    "Check that the region parameter is valid (e.g., 'us-east-1', 'eu-west-1')",
                    "Verify all required parameters are provided",
                    "Ensure parameter values are within expected formats"
                },
                errorType = "InvalidParameter"
            };
        }
        else
        {
            error = new
            {
                success = false,
                error = $"Unexpected error during {operation}",
                details = ex.Message,
                suggestedActions = new[]
                {
                    "Check the operation parameters are correct",
                    "Verify your AWS configuration and credentials",
                    "Try the operation again after a brief wait",
                    "Contact support if the issue persists",
                    $"Exception type: {ex.GetType().Name}"
                },
                errorType = "Unexpected",
                exceptionType = ex.GetType().Name
            };
        }

        return JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion
}