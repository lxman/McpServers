using System.ComponentModel;
using System.Text.Json;
using Amazon.ECR.Model;
using AwsServer.Core.Services.ECR;
using AwsServer.Core.Services.ECR.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AwsMcp.McpTools;

/// <summary>
/// MCP tools for AWS ECR operations
/// </summary>
[McpServerToolType]
public class EcrTools(
    EcrService ecrService,
    ILogger<EcrTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_ecr_repositories")]
    [Description("List ECR repositories. See skills/aws/ecr/list-repositories.md only when using this tool")]
    public async Task<string> ListEcrRepositories()
    {
        try
        {
            logger.LogDebug("Listing ECR repositories");
            DescribeRepositoriesResponse response = await ecrService.ListRepositoriesAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryCount = response.Repositories.Count,
                repositories = response.Repositories.Select(r => new
                {
                    name = r.RepositoryName,
                    arn = r.RepositoryArn,
                    uri = r.RepositoryUri,
                    createdAt = r.CreatedAt,
                    imageScanningEnabled = r.ImageScanningConfiguration?.ScanOnPush,
                    encryptionType = r.EncryptionConfiguration?.EncryptionType?.Value
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing ECR repositories");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_ecr_images")]
    [Description("List images in ECR repository. See skills/aws/ecr/list-images.md only when using this tool")]
    public async Task<string> ListEcrImages(
        string repositoryName,
        int maxResults = 100)
    {
        try
        {
            logger.LogDebug("Listing images in ECR repository {RepositoryName}", repositoryName);
            ListImagesResult response = await ecrService.ListImagesAsync(repositoryName, maxResults);

            return JsonSerializer.Serialize(new
            {
                success = true,
                imageCount = response.ImageIds.Count,
                images = response.ImageIds.Select(i => new
                {
                    imageDigest = i.ImageDigest,
                    imageTag = i.ImageTag
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing images in ECR repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("describe_ecr_images")]
    [Description("Describe ECR images. See skills/aws/ecr/describe-images.md only when using this tool")]
    public async Task<string> DescribeEcrImages(
        string repositoryName,
        string? imageTags = null,
        string? imageDigest = null)
    {
        try
        {
            logger.LogDebug("Describing images in ECR repository {RepositoryName}", repositoryName);

            // Build image identifiers list if specific images are requested
            List<ImageIdentifier>? imageIds = null;
            if (!string.IsNullOrEmpty(imageTags) || !string.IsNullOrEmpty(imageDigest))
            {
                imageIds = new List<ImageIdentifier>();

                if (!string.IsNullOrEmpty(imageTags))
                {
                    foreach (string tag in imageTags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        imageIds.Add(new ImageIdentifier { ImageTag = tag.Trim() });
                    }
                }

                if (!string.IsNullOrEmpty(imageDigest))
                {
                    imageIds.Add(new ImageIdentifier { ImageDigest = imageDigest });
                }
            }

            DescribeImagesResponse response = await ecrService.DescribeImagesAsync(repositoryName, imageIds);

            return JsonSerializer.Serialize(new
            {
                success = true,
                imageCount = response.ImageDetails.Count,
                images = response.ImageDetails.Select(i => new
                {
                    registryId = i.RegistryId,
                    repositoryName = i.RepositoryName,
                    imageTags = i.ImageTags,
                    imageDigest = i.ImageDigest,
                    imageSizeInBytes = i.ImageSizeInBytes,
                    imagePushedAt = i.ImagePushedAt,
                    imageScanStatus = i.ImageScanStatus?.Status?.Value,
                    imageScanFindingsSummary = i.ImageScanFindingsSummary == null ? null : new
                    {
                        vulnerabilitySourceUpdatedAt = i.ImageScanFindingsSummary.VulnerabilitySourceUpdatedAt,
                        imageScanCompletedAt = i.ImageScanFindingsSummary.ImageScanCompletedAt,
                        findingSeverityCounts = i.ImageScanFindingsSummary.FindingSeverityCounts
                    }
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing images in ECR repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_ecr_repository")]
    [Description("Create ECR repository. See skills/aws/ecr/create-repository.md only when using this tool")]
    public async Task<string> CreateEcrRepository(
        string repositoryName,
        bool scanOnPush = true)
    {
        try
        {
            logger.LogDebug("Creating ECR repository {RepositoryName}", repositoryName);

            // Create ImageScanningConfiguration
            var scanConfig = new ImageScanningConfiguration
            {
                ScanOnPush = scanOnPush
            };

            CreateRepositoryResponse response = await ecrService.CreateRepositoryAsync(repositoryName, scanConfig);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repository = new
                {
                    name = response.Repository.RepositoryName,
                    arn = response.Repository.RepositoryArn,
                    uri = response.Repository.RepositoryUri,
                    createdAt = response.Repository.CreatedAt
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating ECR repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_ecr_repository")]
    [Description("Delete ECR repository. See skills/aws/ecr/delete-repository.md only when using this tool")]
    public async Task<string> DeleteEcrRepository(
        string repositoryName,
        bool force = false)
    {
        try
        {
            logger.LogDebug("Deleting ECR repository {RepositoryName}", repositoryName);
            DeleteRepositoryResponse response = await ecrService.DeleteRepositoryAsync(repositoryName, force);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Repository deleted successfully",
                repository = new
                {
                    name = response.Repository.RepositoryName,
                    arn = response.Repository.RepositoryArn
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting ECR repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("describe_ecr_repositories")]
    [Description("Describe ECR repositories. See skills/aws/ecr/describe-repositories.md only when using this tool")]
    public async Task<string> DescribeEcrRepositories(List<string>? repositoryNames = null)
    {
        try
        {
            logger.LogDebug("Describing ECR repositories");
            DescribeRepositoriesResponse response = await ecrService.DescribeRepositoriesAsync(repositoryNames);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryCount = response.Repositories.Count,
                repositories = response.Repositories.Select(r => new
                {
                    name = r.RepositoryName,
                    arn = r.RepositoryArn,
                    uri = r.RepositoryUri,
                    createdAt = r.CreatedAt,
                    imageScanningEnabled = r.ImageScanningConfiguration?.ScanOnPush,
                    encryptionType = r.EncryptionConfiguration?.EncryptionType?.Value,
                    imageTagMutability = r.ImageTagMutability?.Value
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error describing ECR repositories");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("batch_delete_ecr_images")]
    [Description("Batch delete ECR images. See skills/aws/ecr/batch-delete-images.md only when using this tool")]
    public async Task<string> BatchDeleteEcrImages(string repositoryName, List<string> imageTags)
    {
        try
        {
            logger.LogDebug("Batch deleting images from ECR repository {RepositoryName}", repositoryName);
            List<ImageIdentifier> imageIds = imageTags.Select(tag => new ImageIdentifier { ImageTag = tag }).ToList();
            BatchDeleteImageResponse response = await ecrService.BatchDeleteImageAsync(repositoryName, imageIds);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Images deleted successfully",
                deletedImages = response.ImageIds.Select(i => new
                {
                    imageDigest = i.ImageDigest,
                    imageTag = i.ImageTag
                }),
                failures = response.Failures.Select(f => new
                {
                    imageId = new
                    {
                        imageDigest = f.ImageId?.ImageDigest,
                        imageTag = f.ImageId?.ImageTag
                    },
                    failureCode = f.FailureCode?.Value,
                    failureReason = f.FailureReason
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error batch deleting images from ECR repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_ecr_authorization_token")]
    [Description("Get ECR authorization token. See skills/aws/ecr/get-authorization-token.md only when using this tool")]
    public async Task<string> GetEcrAuthorizationToken()
    {
        try
        {
            logger.LogDebug("Getting ECR authorization token");
            GetAuthorizationTokenResponse response = await ecrService.GetAuthorizationTokenAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                authorizationData = response.AuthorizationData.Select(a => new
                {
                    authorizationToken = a.AuthorizationToken,
                    proxyEndpoint = a.ProxyEndpoint,
                    expiresAt = a.ExpiresAt
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting ECR authorization token");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_ecr_lifecycle_policy")]
    [Description("Get ECR lifecycle policy. See skills/aws/ecr/get-lifecycle-policy.md only when using this tool")]
    public async Task<string> GetEcrLifecyclePolicy(string repositoryName)
    {
        try
        {
            logger.LogDebug("Getting lifecycle policy for ECR repository {RepositoryName}", repositoryName);
            GetLifecyclePolicyResponse response = await ecrService.GetLifecyclePolicyAsync(repositoryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                registryId = response.RegistryId,
                repositoryName = response.RepositoryName,
                lifecyclePolicyText = response.LifecyclePolicyText,
                lastEvaluatedAt = response.LastEvaluatedAt
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting lifecycle policy for ECR repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("put_ecr_lifecycle_policy")]
    [Description("Put ECR lifecycle policy. See skills/aws/ecr/put-lifecycle-policy.md only when using this tool")]
    public async Task<string> PutEcrLifecyclePolicy(string repositoryName, string lifecyclePolicyText)
    {
        try
        {
            logger.LogDebug("Putting lifecycle policy for ECR repository {RepositoryName}", repositoryName);
            PutLifecyclePolicyResponse response = await ecrService.PutLifecyclePolicyAsync(repositoryName, lifecyclePolicyText);

            return JsonSerializer.Serialize(new
            {
                success = true,
                registryId = response.RegistryId,
                repositoryName = response.RepositoryName,
                lifecyclePolicyText = response.LifecyclePolicyText
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting lifecycle policy for ECR repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}