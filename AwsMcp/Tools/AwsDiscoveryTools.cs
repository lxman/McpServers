using System.ComponentModel;
using System.Text.Json;
using AwsMcp.Common;
using AwsMcp.Configuration;
using AwsMcp.Configuration.Models;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

[McpServerToolType]
public class AwsDiscoveryTools(AwsDiscoveryService discoveryService)
{
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

            bool success = discoveryService.Initialize(config);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "AWS Discovery service initialized successfully" : "Failed to initialize AWS Discovery service",
                region,
                usingProfile = !string.IsNullOrEmpty(profileName),
                usingCustomEndpoint = !string.IsNullOrEmpty(serviceUrl)
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "initializing AWS Discovery service");
        }
    }
    
    [McpServerTool]
    [Description("Get current AWS account information with auto-discovered credentials")]
    public async Task<string> GetAccountInfoAuto()
    {
        try
        {
            // Try auto-initialization first
            if (!discoveryService.AutoInitialize())
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Could not auto-discover AWS credentials",
                    suggestions = new[]
                    {
                        "Run 'aws configure' to set up credentials",
                        "Ensure AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY are set",
                        "Verify your AWS CLI is working with 'aws sts get-caller-identity'"
                    }
                });
            }
        
            // Now get account info
            AccountInfo accountInfo = await discoveryService.GetAccountInfoAsync();
        
            return JsonSerializer.Serialize(new
            {
                success = true,
                autoDiscovered = true,
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "auto-discovering AWS account information");
        }
    }

    [McpServerTool]
    [Description("Get current AWS account information and caller identity using STS")]
    public async Task<string> GetAccountInfo()
    {
        try
        {
            AccountInfo accountInfo = await discoveryService.GetAccountInfoAsync();
            
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
            }, SerializerOptions.JsonOptionsIndented);
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
            AutoDiscoveryResult result = await discoveryService.AutoDiscoverConfigurationAsync();
            
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
            }, SerializerOptions.JsonOptionsIndented);
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
            List<ServicePermissionTest> results = await discoveryService.TestServicePermissionsAsync(region);
            
            List<ServicePermissionTest> workingServices = results.Where(r => r.HasPermission).ToList();
            List<ServicePermissionTest> failedServices = results.Where(r => !r.HasPermission).ToList();
            
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
            }, SerializerOptions.JsonOptionsIndented);
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
            CliConfiguration cliConfig = discoveryService.DetectCliConfiguration();
            
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
            }, SerializerOptions.JsonOptionsIndented);
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
            AutoDiscoveryResult discoveryResult = await discoveryService.AutoDiscoverConfigurationAsync();
            
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
            
            return JsonSerializer.Serialize(analysis, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "analyzing AWS environment");
        }
    }
    
    [McpServerTool]
    [Description("Get help with finding and configuring AWS credentials - start here if you're unsure about your setup")]
    public static string CredentialsHelp()
    {
        var helpInfo = new
        {
            title = "AWS Credentials Discovery & Setup Guide",
            version = "1.0",
            lastUpdated = "2025-09-12",
            
            quickStart = new
            {
                description = "If you're not sure about your AWS setup, follow these steps:",
                steps = new[]
                {
                    "1. Run aws:detect_cli_configuration (works without credentials)",
                    "2. If profiles found → Run aws:initialize_aws_discovery with profileName",
                    "3. If no profiles → Follow the setup guide below",
                    "4. Run aws:auto_discover_configuration for complete analysis"
                }
            },
            
            credentialSources = new
            {
                description = "AWS credentials can be found in several places (checked in this order):",
                sources = new[]
                {
                    new
                    {
                        source = "Environment Variables",
                        priority = 1,
                        variables = new[] { "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_REGION", "AWS_PROFILE" },
                        location = string.Empty,
                        checkCommand = "aws:detect_cli_configuration",
                        setup = "Set environment variables directly or use 'aws configure set' commands"
                    },
                    new
                    {
                        source = "AWS CLI Profiles",
                        priority = 2,
                        variables = Array.Empty<string>(),
                        location = "~/.aws/credentials and ~/.aws/config",
                        checkCommand = "aws:detect_cli_configuration",
                        setup = "Run 'aws configure' or 'aws configure --profile profile-name'"
                    },
                    new
                    {
                        source = "IAM Instance Profile",
                        priority = 3,
                        variables = Array.Empty<string>(),
                        location = "EC2 metadata service",
                        checkCommand = "aws:get_account_info (after initialization)",
                        setup = "Attach IAM role to EC2 instance"
                    },
                    new
                    {
                        source = "AWS SSO",
                        priority = 4,
                        variables = Array.Empty<string>(),
                        location = "SSO session cache",
                        checkCommand = "aws:detect_cli_configuration",
                        setup = "Run 'aws sso login'"
                    }
                }
            },
            
            commonScenarios = new
            {
                description = "Common credential scenarios and solutions:",
                scenarios = new[]
                {
                    new
                    {
                        scenario = "No AWS credentials found",
                        detection = "aws:detect_cli_configuration returns no profiles",
                        solution = new[]
                        {
                            "Run 'aws configure' to set up credentials",
                            "Or set environment variables: AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY",
                            "Or install AWS CLI and run 'aws configure'"
                        }
                    },
                    new
                    {
                        scenario = "AWS CLI profiles exist but incomplete",
                        detection = "Profiles shown but hasCredentials = false",
                        solution = new[]
                        {
                            "Run 'aws configure' to complete the default profile",
                            "Or run 'aws configure --profile profile-name' for specific profiles",
                            "Check ~/.aws/credentials file has access_key_id and secret_access_key"
                        }
                    },
                    new
                    {
                        scenario = "Authentication fails with existing profiles",
                        detection = "aws:get_account_info returns InvalidClientTokenId",
                        solution = new[]
                        {
                            "Check if credentials are expired or deactivated",
                            "Try: aws sts get-caller-identity from command line",
                            "Re-run 'aws configure' with fresh credentials",
                            "Check if you're using the correct AWS account"
                        }
                    },
                    new
                    {
                        scenario = "Partial permissions (some services work, others don't)",
                        detection = "aws:test_service_permissions shows mixed results",
                        solution = new[]
                        {
                            "Use service-specific initialization (e.g., testMetricsOnly=true)",
                            "Contact AWS administrator for additional permissions",
                            "Check IAM policies attached to your user/role"
                        }
                    },
                    new
                    {
                        scenario = "GovCloud or special regions",
                        detection = "Region shows us-gov-* or different endpoint needed",
                        solution = new[]
                        {
                            "Ensure you're using GovCloud-specific endpoints",
                            "Verify your credentials are for the correct AWS partition",
                            "Use region-specific initialization parameters"
                        }
                    }
                }
            },
            
            diagnosticWorkflow = new
            {
                description = "Complete diagnostic workflow for troubleshooting:",
                steps = new[]
                {
                    new
                    {
                        step = 1,
                        command = "aws:detect_cli_configuration",
                        purpose = "Check for AWS CLI setup and profiles",
                        nextStep = "If profiles found → proceed to step 2, else → setup credentials"
                    },
                    new
                    {
                        step = 2,
                        command = "aws:initialize_aws_discovery (with profileName if found)",
                        purpose = "Initialize discovery service with detected credentials",
                        nextStep = "If success → step 3, else → check credential completeness"
                    },
                    new
                    {
                        step = 3,
                        command = "aws:get_account_info",
                        purpose = "Verify AWS authentication and get account details",
                        nextStep = "If success → step 4, else → troubleshoot credentials"
                    },
                    new
                    {
                        step = 4,
                        command = "aws:test_service_permissions",
                        purpose = "Test what AWS services you can access",
                        nextStep = "Use results to guide service-specific initialization"
                    },
                    new
                    {
                        step = 5,
                        command = "aws:auto_discover_configuration",
                        purpose = "Get complete setup analysis and ready-to-use commands",
                        nextStep = "Follow the recommended initialization commands"
                    }
                }
            },
            
            availableTools = new
            {
                description = "All available AWS tools (roughly in order of typical usage):",
                discoveryTools = new[]
                {
                    "aws:credentials_help - This help guide",
                    "aws:detect_cli_configuration - Check AWS CLI setup (no credentials needed)",
                    "aws:initialize_aws_discovery - Initialize discovery service",
                    "aws:get_account_info - Get AWS account information",
                    "aws:auto_discover_configuration - Complete environment analysis",
                    "aws:test_service_permissions - Test service access"
                },
                serviceTools = new[]
                {
                    "aws:initialize_s3 - Initialize S3 service",
                    "aws:initialize_cloud_watch - Initialize CloudWatch service", 
                    "aws:initialize_ecr - Initialize ECR service",
                    "aws:initialize_ecs - Initialize ECS service"
                },
                operationalTools = new[]
                {
                    "S3: list_buckets, list_objects, get_object_content, etc.",
                    "CloudWatch: list_metrics, list_alarms, get_metric_statistics, etc.",
                    "ECR: list_repositories, describe_images, etc.",
                    "ECS: list_clusters, list_services, run_task, etc."
                }
            },
            
            tips = new
            {
                description = "Pro tips for smooth AWS MCP experience:",
                recommendations = new[]
                {
                    "Always start with aws:detect_cli_configuration to understand your setup",
                    "Use aws:auto_discover_configuration for one-command complete analysis",
                    "For partial permissions, use service-specific initialization (testMetricsOnly, testLogsOnly)",
                    "Keep credentials in AWS CLI profiles rather than environment variables for security",
                    "Use descriptive profile names if you have multiple AWS accounts",
                    "Test with aws:test_service_permissions before initializing all services"
                }
            },
            
            troubleshootingResources = new
            {
                awsDocumentation = "https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html",
                credentialSetup = "https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html",
                govCloudSetup = "https://docs.aws.amazon.com/govcloud-us/latest/UserGuide/",
                commonErrors = "Use enhanced error messages - they include specific suggestions for each error type"
            }
        };
        
        return JsonSerializer.Serialize(helpInfo, SerializerOptions.JsonOptionsIndented);
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
            List<AwsProfile> incompleteProfiles = cliConfig.Profiles.Where(p => !p.HasAccessKey || !p.HasSecretKey).ToList();
            if (incompleteProfiles.Count != 0)
            {
                recommendations.Add($"Incomplete profiles found: {string.Join(", ", incompleteProfiles.Select(p => p.Name))}");
                recommendations.Add("Complete profiles need both aws_access_key_id and aws_secret_access_key");
            }
            
            List<AwsProfile> completeProfiles = cliConfig.Profiles.Where(p => p is { HasAccessKey: true, HasSecretKey: true }).ToList();
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
            ServicePermissionTest? cwMetrics = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "CloudWatch Metrics");
            ServicePermissionTest? cwLogs = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "CloudWatch Logs");
            
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
            ServicePermissionTest? s3Permission = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "S3");
            if (s3Permission?.HasPermission == true)
            {
                commands["s3"] = new
                {
                    tool = "InitializeS3",
                    parameters = new { region = result.AccountInfo.InferredRegion },
                    description = "S3 service initialization"
                };
            }
            
            ServicePermissionTest? ecrPermission = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "ECR");
            if (ecrPermission?.HasPermission == true)
            {
                commands["ecr"] = new
                {
                    tool = "InitializeECR",
                    parameters = new { region = result.AccountInfo.InferredRegion },
                    description = "ECR service initialization"
                };
            }
            
            ServicePermissionTest? ecsPermission = result.ServicePermissions.FirstOrDefault(s => s.ServiceName == "ECS");
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
            string[] actions = awsEx.ErrorCode switch
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

        return JsonSerializer.Serialize(error, SerializerOptions.JsonOptionsIndented);
    }

    #endregion
}