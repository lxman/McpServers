using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.Runtime;
using AwsMcp.Configuration;
using AwsMcp.Configuration.Models;
using Microsoft.Extensions.Logging;

namespace AwsMcp.ECR;

/// <summary>
/// Service for ECR operations
/// </summary>
public class EcrService
{
    private readonly ILogger<EcrService> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonECRClient? _ecrClient;
    private AwsConfiguration? _config;
    private bool _isInitialized;
    
    public EcrService(
        ILogger<EcrService> logger,
        AwsDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        
        _ = Task.Run(AutoInitializeAsync);
    }
    
    /// <summary>
    /// Initialize ECR client with configuration
    /// </summary>
    public async Task<bool> InitializeAsync(AwsConfiguration config)
    {
        try
        {
            _config = config;
            
            var clientConfig = new AmazonECRConfig
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
            AWSCredentials? credentials = credentialsProvider.GetCredentials();
            
            if (credentials != null)
            {
                _ecrClient = new AmazonECRClient(credentials, clientConfig);
            }
            else
            {
                _ecrClient = new AmazonECRClient(clientConfig);
            }
            
            // Test connection by describing repositories
            await _ecrClient.DescribeRepositoriesAsync(new DescribeRepositoriesRequest());
            
            _logger.LogInformation("ECR client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ECR client");
            return false;
        }
    }
    
    /// <summary>
    /// List all repositories
    /// </summary>
    public async Task<DescribeRepositoriesResponse> ListRepositoriesAsync()
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.DescribeRepositoriesAsync(new DescribeRepositoriesRequest());
    }
    
    /// <summary>
    /// Describe specific repositories
    /// </summary>
    public async Task<DescribeRepositoriesResponse> DescribeRepositoriesAsync(List<string>? repositoryNames = null)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeRepositoriesRequest();
        if (repositoryNames != null && repositoryNames.Count != 0)
        {
            request.RepositoryNames = repositoryNames;
        }
        
        return await _ecrClient!.DescribeRepositoriesAsync(request);
    }
    
    /// <summary>
    /// Create a new repository
    /// </summary>
    public async Task<CreateRepositoryResponse> CreateRepositoryAsync(string repositoryName, 
        ImageScanningConfiguration? imageScanningConfiguration = null,
        List<Tag>? tags = null)
    {
        await EnsureInitializedAsync();
        
        var request = new CreateRepositoryRequest
        {
            RepositoryName = repositoryName
        };
        
        if (imageScanningConfiguration != null)
        {
            request.ImageScanningConfiguration = imageScanningConfiguration;
        }
        
        if (tags != null && tags.Count != 0)
        {
            request.Tags = tags;
        }
        
        return await _ecrClient!.CreateRepositoryAsync(request);
    }
    
    /// <summary>
    /// Delete a repository
    /// </summary>
    public async Task<DeleteRepositoryResponse> DeleteRepositoryAsync(string repositoryName, bool force = false)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.DeleteRepositoryAsync(new DeleteRepositoryRequest 
        { 
            RepositoryName = repositoryName,
            Force = force
        });
    }
    
    /// <summary>
    /// List images in a repository
    /// </summary>
    public async Task<ListImagesResponse> ListImagesAsync(string repositoryName)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.ListImagesAsync(new ListImagesRequest 
        { 
            RepositoryName = repositoryName 
        });
    }
    
    /// <summary>
    /// Describe images in a repository
    /// </summary>
    public async Task<DescribeImagesResponse> DescribeImagesAsync(string repositoryName, 
        List<ImageIdentifier>? imageIds = null)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeImagesRequest
        {
            RepositoryName = repositoryName
        };
        
        if (imageIds != null && imageIds.Count != 0)
        {
            request.ImageIds = imageIds;
        }
        
        return await _ecrClient!.DescribeImagesAsync(request);
    }
    
    /// <summary>
    /// Get authorization token for Docker login
    /// </summary>
    public async Task<GetAuthorizationTokenResponse> GetAuthorizationTokenAsync()
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
    }
    
    /// <summary>
    /// Batch delete images
    /// </summary>
    public async Task<BatchDeleteImageResponse> BatchDeleteImageAsync(string repositoryName, 
        List<ImageIdentifier> imageIds)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.BatchDeleteImageAsync(new BatchDeleteImageRequest
        {
            RepositoryName = repositoryName,
            ImageIds = imageIds
        });
    }
    
    /// <summary>
    /// Put image
    /// </summary>
    public async Task<PutImageResponse> PutImageAsync(string repositoryName, 
        string imageManifest, string? imageTag = null)
    {
        await EnsureInitializedAsync();
        
        var request = new PutImageRequest
        {
            RepositoryName = repositoryName,
            ImageManifest = imageManifest
        };
        
        if (!string.IsNullOrEmpty(imageTag))
        {
            request.ImageTag = imageTag;
        }
        
        return await _ecrClient!.PutImageAsync(request);
    }
    
    /// <summary>
    /// Get repository policy
    /// </summary>
    public async Task<GetRepositoryPolicyResponse> GetRepositoryPolicyAsync(string repositoryName)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.GetRepositoryPolicyAsync(new GetRepositoryPolicyRequest 
        { 
            RepositoryName = repositoryName 
        });
    }
    
    /// <summary>
    /// Set repository policy
    /// </summary>
    public async Task<SetRepositoryPolicyResponse> SetRepositoryPolicyAsync(string repositoryName, 
        string policyText)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.SetRepositoryPolicyAsync(new SetRepositoryPolicyRequest
        {
            RepositoryName = repositoryName,
            PolicyText = policyText
        });
    }
    
    /// <summary>
    /// Delete repository policy
    /// </summary>
    public async Task<DeleteRepositoryPolicyResponse> DeleteRepositoryPolicyAsync(string repositoryName)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.DeleteRepositoryPolicyAsync(new DeleteRepositoryPolicyRequest 
        { 
            RepositoryName = repositoryName 
        });
    }
    
    /// <summary>
    /// Describe image scan findings
    /// </summary>
    public async Task<DescribeImageScanFindingsResponse> DescribeImageScanFindingsAsync(
        string repositoryName, ImageIdentifier imageId)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.DescribeImageScanFindingsAsync(new DescribeImageScanFindingsRequest
        {
            RepositoryName = repositoryName,
            ImageId = imageId
        });
    }
    
    /// <summary>
    /// Start image scan
    /// </summary>
    public async Task<StartImageScanResponse> StartImageScanAsync(string repositoryName, 
        ImageIdentifier imageId)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.StartImageScanAsync(new StartImageScanRequest
        {
            RepositoryName = repositoryName,
            ImageId = imageId
        });
    }
    
    /// <summary>
    /// Get lifecycle policy
    /// </summary>
    public async Task<GetLifecyclePolicyResponse> GetLifecyclePolicyAsync(string repositoryName)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.GetLifecyclePolicyAsync(new GetLifecyclePolicyRequest 
        { 
            RepositoryName = repositoryName 
        });
    }
    
    /// <summary>
    /// Put lifecycle policy
    /// </summary>
    public async Task<PutLifecyclePolicyResponse> PutLifecyclePolicyAsync(string repositoryName, 
        string lifecyclePolicyText)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.PutLifecyclePolicyAsync(new PutLifecyclePolicyRequest
        {
            RepositoryName = repositoryName,
            LifecyclePolicyText = lifecyclePolicyText
        });
    }
    
    /// <summary>
    /// Delete lifecycle policy
    /// </summary>
    public async Task<DeleteLifecyclePolicyResponse> DeleteLifecyclePolicyAsync(string repositoryName)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.DeleteLifecyclePolicyAsync(new DeleteLifecyclePolicyRequest 
        { 
            RepositoryName = repositoryName 
        });
    }
    
    /// <summary>
    /// Tag resource
    /// </summary>
    public async Task<TagResourceResponse> TagResourceAsync(string resourceArn, List<Tag> tags)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.TagResourceAsync(new TagResourceRequest
        {
            ResourceArn = resourceArn,
            Tags = tags
        });
    }
    
    /// <summary>
    /// Untag resource
    /// </summary>
    public async Task<UntagResourceResponse> UntagResourceAsync(string resourceArn, List<string> tagKeys)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.UntagResourceAsync(new UntagResourceRequest
        {
            ResourceArn = resourceArn,
            TagKeys = tagKeys
        });
    }
    
    /// <summary>
    /// List tags for resource
    /// </summary>
    public async Task<ListTagsForResourceResponse> ListTagsForResourceAsync(string resourceArn)
    {
        await EnsureInitializedAsync();
        return await _ecrClient!.ListTagsForResourceAsync(new ListTagsForResourceRequest 
        { 
            ResourceArn = resourceArn 
        });
    }
    
    /// <summary>
    /// Auto-initialize with discovered credentials
    /// </summary>
    private async Task AutoInitializeAsync()
    {
        try
        {
            if (_discoveryService.AutoInitialize())
            {
                AccountInfo accountInfo = await _discoveryService.GetAccountInfoAsync();
                
                var config = new AwsConfiguration
                {
                    Region = accountInfo.InferredRegion,
                    ProfileName = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default"
                };
                
                await InitializeAsync(config);
                _isInitialized = true;
                _logger.LogInformation("S3 service auto-initialized successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-initialize S3 service. Explicit initialization may be required.");
        }
    }
    
    /// <summary>
    /// Ensure service is initialized (wait if auto-initialization is still running)
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        // Wait for auto-initialization to complete
        if (!_isInitialized && _ecrClient == null)
        {
            // Wait up to 5 seconds for auto-initialization
            DateTime timeout = DateTime.UtcNow.AddSeconds(5);
            while (!_isInitialized && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
            
            if (_ecrClient == null)
            {
                throw new InvalidOperationException(
                    "S3 client could not be auto-initialized. Please ensure AWS credentials are configured properly or call Initialize explicitly.");
            }
        }
    }
    
    public void Dispose()
    {
        _ecrClient?.Dispose();
    }
}
