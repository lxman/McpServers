using System.ComponentModel;
using System.Text.Json;
using Amazon.ECR.Model;
using AwsMcp.Common;
using AwsMcp.Configuration;
using AwsMcp.ECR;
using AwsMcp.ECR.Models;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

[McpServerToolType]
public class EcrTools(EcrService ecrService)
{
    [McpServerTool]
    [Description("Initialize ECR service with AWS credentials and configuration")]
    public async Task<string> InitializeEcr(
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

            bool success = await ecrService.InitializeAsync(config);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "ECR service initialized successfully" : "Failed to initialize ECR service",
                region = config.Region
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List all ECR repositories")]
    public async Task<string> ListRepositories()
    {
        try
        {
            DescribeRepositoriesResponse response = await ecrService.ListRepositoriesAsync();
            
            var repositories = response.Repositories.Select(repo => new
            {
                repo.RepositoryName,
                repo.RepositoryArn,
                repo.RepositoryUri,
                repo.RegistryId,
                repo.CreatedAt,
                ImageScanningConfiguration = repo.ImageScanningConfiguration != null ? new
                {
                    repo.ImageScanningConfiguration.ScanOnPush
                } : null
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositories,
                count = response.Repositories.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Describe specific ECR repositories")]
    public async Task<string> DescribeRepositories(
        [Description("Comma-separated list of repository names to describe (optional, describes all if not provided)")]
        string? repositoryNames = null)
    {
        try
        {
            List<string>? repositories = null;
            if (!string.IsNullOrEmpty(repositoryNames))
            {
                repositories = repositoryNames.Split(',').Select(r => r.Trim()).ToList();
            }

            DescribeRepositoriesResponse response = await ecrService.DescribeRepositoriesAsync(repositories);
            
            var repositoryDetails = response.Repositories.Select(repo => new
            {
                repo.RepositoryName,
                repo.RepositoryArn,
                repo.RepositoryUri,
                repo.RegistryId,
                repo.CreatedAt,
                ImageScanningConfiguration = repo.ImageScanningConfiguration != null ? new
                {
                    repo.ImageScanningConfiguration.ScanOnPush
                } : null,
                ImageTagMutability = repo.ImageTagMutability?.ToString(),
                EncryptionConfiguration = repo.EncryptionConfiguration != null ? new
                {
                    EncryptionType = repo.EncryptionConfiguration.EncryptionType?.ToString(), repo.EncryptionConfiguration.KmsKey
                } : null
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositories = repositoryDetails,
                count = response.Repositories.Count
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Create a new ECR repository")]
    public async Task<string> CreateRepository(
        [Description("Name of the repository to create")]
        string repositoryName,
        [Description("Enable image scanning on push (default: false)")]
        bool imageScanOnPush = false,
        [Description("Tags to apply to the repository (JSON format: [{\"Key\":\"Environment\",\"Value\":\"Production\"}])")]
        string? tags = null)
    {
        try
        {
            ImageScanningConfiguration? scanConfig = null;
            if (imageScanOnPush)
            {
                scanConfig = new ImageScanningConfiguration { ScanOnPush = true };
            }

            List<Tag>? repositoryTags = null;
            if (!string.IsNullOrEmpty(tags))
            {
                var tagData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(tags);
                repositoryTags = tagData?.Select(t => new Tag { Key = t["Key"], Value = t["Value"] }).ToList();
            }

            CreateRepositoryResponse response = await ecrService.CreateRepositoryAsync(repositoryName, scanConfig, repositoryTags);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repository = new
                {
                    response.Repository.RepositoryName,
                    response.Repository.RepositoryArn,
                    response.Repository.RepositoryUri,
                    response.Repository.RegistryId,
                    response.Repository.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Delete an ECR repository")]
    public async Task<string> DeleteRepository(
        [Description("Name of the repository to delete")]
        string repositoryName,
        [Description("Force delete even if repository contains images (default: false)")]
        bool force = false)
    {
        try
        {
            DeleteRepositoryResponse response = await ecrService.DeleteRepositoryAsync(repositoryName, force);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repository = new
                {
                    response.Repository.RepositoryName,
                    response.Repository.RepositoryArn,
                    response.Repository.RegistryId
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List images in an ECR repository with pagination support. Returns one page of results at a time to avoid timeouts with repositories containing many image tags.")]
    public async Task<string> ListImages(
        [Description("Name of the repository")]
        string repositoryName,
        [Description("Maximum number of images to return per page (1-1000, default: 100)")]
        int maxResults = 100,
        [Description("Continuation token from previous response (for pagination). Leave empty for first request.")]
        string? nextToken = null)
    {
        try
        {
            ListImagesResult result = await ecrService.ListImagesAsync(
                repositoryName,
                maxResults,
                nextToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName = result.RepositoryName,
                imageCount = result.ImageCount,
                hasMoreResults = result.HasMoreResults,
                nextToken = result.NextToken,
                summary = result.Summary,
                imageIds = result.ImageIds.Select(img => new
                {
                    imageDigest = img.ImageDigest,
                    imageTag = img.ImageTag
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Error listing ECR images: {ex.Message}",
                repositoryName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Describe images in an ECR repository with detailed information")]
    public async Task<string> DescribeImages(
        [Description("Name of the repository")]
        string repositoryName,
        [Description("Comma-separated list of image tags to describe (optional, describes all if not provided)")]
        string? imageTags = null)
    {
        try
        {
            List<ImageIdentifier>? imageIds = null;
            if (!string.IsNullOrEmpty(imageTags))
            {
                List<string> tags = imageTags.Split(',').Select(t => t.Trim()).ToList();
                imageIds = tags.Select(tag => new ImageIdentifier { ImageTag = tag }).ToList();
            }

            DescribeImagesResponse response = await ecrService.DescribeImagesAsync(repositoryName, imageIds);
            
            var imageDetails = response.ImageDetails.Select(image => new
            {
                image.RegistryId,
                image.RepositoryName,
                image.ImageDigest,
                image.ImageTags,
                image.ImageSizeInBytes,
                image.ImagePushedAt,
                ImageScanFindingsSummary = image.ImageScanFindingsSummary != null ? new
                {
                    image.ImageScanFindingsSummary.ImageScanCompletedAt, image.ImageScanFindingsSummary.VulnerabilitySourceUpdatedAt
                } : null,
                image.ArtifactMediaType,
                image.ImageManifestMediaType
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                images = imageDetails,
                count = response.ImageDetails.Count,
                repositoryName
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get authorization token for Docker login to ECR")]
    public async Task<string> GetAuthorizationToken()
    {
        try
        {
            GetAuthorizationTokenResponse response = await ecrService.GetAuthorizationTokenAsync();
            
            var authData = response.AuthorizationData.Select(auth => new
            {
                auth.AuthorizationToken,
                auth.ExpiresAt,
                auth.ProxyEndpoint
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                authorizationData = authData
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Batch delete images from an ECR repository")]
    public async Task<string> BatchDeleteImages(
        [Description("Name of the repository")]
        string repositoryName,
        [Description("Comma-separated list of image tags to delete")]
        string imageTags)
    {
        try
        {
            List<string> tags = imageTags.Split(',').Select(t => t.Trim()).ToList();
            List<ImageIdentifier> imageIds = tags.Select(tag => new ImageIdentifier { ImageTag = tag }).ToList();

            BatchDeleteImageResponse response = await ecrService.BatchDeleteImageAsync(repositoryName, imageIds);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                imageIds = response.ImageIds?.Select(id => new
                {
                    id.ImageDigest, id.ImageTag
                }),
                failures = response.Failures?.Select(f => new
                {
                    ImageId = f.ImageId != null ? new
                    {
                        f.ImageId.ImageDigest, f.ImageId.ImageTag
                    } : null,
                    FailureCode = f.FailureCode?.ToString(),
                    f.FailureReason
                }),
                repositoryName
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get repository policy")]
    public async Task<string> GetRepositoryPolicy(
        [Description("Name of the repository")]
        string repositoryName)
    {
        try
        {
            GetRepositoryPolicyResponse response = await ecrService.GetRepositoryPolicyAsync(repositoryName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                registryId = response.RegistryId,
                policyText = response.PolicyText
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Set repository policy")]
    public async Task<string> SetRepositoryPolicy(
        [Description("Name of the repository")]
        string repositoryName,
        [Description("Policy document in JSON format")]
        string policyText)
    {
        try
        {
            SetRepositoryPolicyResponse response = await ecrService.SetRepositoryPolicyAsync(repositoryName, policyText);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                registryId = response.RegistryId,
                policyText = response.PolicyText
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Delete repository policy")]
    public async Task<string> DeleteRepositoryPolicy(
        [Description("Name of the repository")]
        string repositoryName)
    {
        try
        {
            DeleteRepositoryPolicyResponse response = await ecrService.DeleteRepositoryPolicyAsync(repositoryName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                registryId = response.RegistryId,
                policyText = response.PolicyText
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Describe image scan findings")]
    public async Task<string> DescribeImageScanFindings(
        [Description("Name of the repository")]
        string repositoryName,
        [Description("Image tag to scan")]
        string imageTag)
    {
        try
        {
            var imageId = new ImageIdentifier { ImageTag = imageTag };
            DescribeImageScanFindingsResponse response = await ecrService.DescribeImageScanFindingsAsync(repositoryName, imageId);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                imageId = new
                {
                    response.ImageId.ImageDigest, response.ImageId.ImageTag
                },
                imageScanStatus = response.ImageScanStatus != null ? new
                {
                    Status = response.ImageScanStatus.Status?.ToString(), response.ImageScanStatus.Description
                } : null,
                imageScanFindings = response.ImageScanFindings != null ? new
                {
                    Findings = response.ImageScanFindings.Findings?.Select(finding => new
                    {
                        finding.Name,
                        finding.Description,
                        finding.Uri,
                        Severity = finding.Severity?.ToString(),
                        Attributes = finding.Attributes?.Select(attr => new
                        {
                            attr.Key, attr.Value
                        })
                    })
                } : null,
                registryId = response.RegistryId
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Start image scan")]
    public async Task<string> StartImageScan(
        [Description("Name of the repository")]
        string repositoryName,
        [Description("Image tag to scan")]
        string imageTag)
    {
        try
        {
            var imageId = new ImageIdentifier { ImageTag = imageTag };
            StartImageScanResponse response = await ecrService.StartImageScanAsync(repositoryName, imageId);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                imageId = new
                {
                    response.ImageId.ImageDigest, response.ImageId.ImageTag
                },
                imageScanStatus = response.ImageScanStatus != null ? new
                {
                    Status = response.ImageScanStatus.Status?.ToString(), response.ImageScanStatus.Description
                } : null,
                registryId = response.RegistryId
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get lifecycle policy")]
    public async Task<string> GetLifecyclePolicy(
        [Description("Name of the repository")]
        string repositoryName)
    {
        try
        {
            GetLifecyclePolicyResponse response = await ecrService.GetLifecyclePolicyAsync(repositoryName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                registryId = response.RegistryId,
                lifecyclePolicyText = response.LifecyclePolicyText,
                lastEvaluatedAt = response.LastEvaluatedAt
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Put lifecycle policy")]
    public async Task<string> PutLifecyclePolicy(
        [Description("Name of the repository")]
        string repositoryName,
        [Description("Lifecycle policy document in JSON format")]
        string lifecyclePolicyText)
    {
        try
        {
            PutLifecyclePolicyResponse response = await ecrService.PutLifecyclePolicyAsync(repositoryName, lifecyclePolicyText);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                registryId = response.RegistryId
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Delete lifecycle policy")]
    public async Task<string> DeleteLifecyclePolicy(
        [Description("Name of the repository")]
        string repositoryName)
    {
        try
        {
            DeleteLifecyclePolicyResponse response = await ecrService.DeleteLifecyclePolicyAsync(repositoryName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                registryId = response.RegistryId,
                lifecyclePolicyText = response.LifecyclePolicyText,
                lastEvaluatedAt = response.LastEvaluatedAt
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Tag an ECR resource")]
    public async Task<string> TagResource(
        [Description("ARN of the resource to tag")]
        string resourceArn,
        [Description("Tags to apply (JSON format: [{\"Key\":\"Environment\",\"Value\":\"Production\"}])")]
        string tags)
    {
        try
        {
            var tagData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(tags);
            List<Tag>? resourceTags = tagData?.Select(t => new Tag { Key = t["Key"], Value = t["Value"] }).ToList();

            if (resourceTags == null || resourceTags.Count == 0)
            {
                throw new ArgumentException("No valid tags provided");
            }

            TagResourceResponse response = await ecrService.TagResourceAsync(resourceArn, resourceTags);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceArn
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Untag an ECR resource")]
    public async Task<string> UntagResource(
        [Description("ARN of the resource to untag")]
        string resourceArn,
        [Description("Comma-separated list of tag keys to remove")]
        string tagKeys)
    {
        try
        {
            List<string> keys = tagKeys.Split(',').Select(k => k.Trim()).ToList();
            UntagResourceResponse response = await ecrService.UntagResourceAsync(resourceArn, keys);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceArn
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List tags for an ECR resource")]
    public async Task<string> ListTagsForResource(
        [Description("ARN of the resource")]
        string resourceArn)
    {
        try
        {
            ListTagsForResourceResponse response = await ecrService.ListTagsForResourceAsync(resourceArn);
            
            var tags = response.Tags?.Select(tag => new
            {
                tag.Key, tag.Value
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceArn,
                tags,
                count = response.Tags?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
