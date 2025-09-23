using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AwsMcp.Configuration;
using AwsMcp.Configuration.Models;
using Microsoft.Extensions.Logging;

namespace AwsMcp.S3;

/// <summary>
/// Service for S3 operations
/// </summary>
public class S3Service
{
    private readonly ILogger<S3Service> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonS3Client? _s3Client;
    private AwsConfiguration? _config;
    private bool _isInitialized;
    
    public S3Service(
        ILogger<S3Service> logger,
        AwsDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _ = Task.Run(AutoInitializeAsync);
    }

    
    /// <summary>
    /// Initialize S3 client with configuration
    /// </summary>
    public async Task<bool> InitializeAsync(AwsConfiguration config)
    {
        try
        {
            _config = config;
            
            var clientConfig = new AmazonS3Config
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps,
                ForcePathStyle = config.ForcePathStyle
            };
            
            // Set custom endpoint if provided (for LocalStack, etc.)
            if (!string.IsNullOrEmpty(config.ServiceUrl))
            {
                clientConfig.ServiceURL = config.ServiceUrl;
            }
            
            var credentialsProvider = new AwsCredentialsProvider(config);
            var credentials = credentialsProvider.GetCredentials();
            
            if (credentials != null)
            {
                _s3Client = new AmazonS3Client(credentials, clientConfig);
            }
            else
            {
                _s3Client = new AmazonS3Client(clientConfig);
            }
            
            // Test connection
            await _s3Client.ListBucketsAsync();
            
            _logger.LogInformation("S3 client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize S3 client");
            return false;
        }
    }
    
    /// <summary>
    /// List all buckets
    /// </summary>
    public async Task<List<S3Bucket>> ListBucketsAsync()
    {
        await EnsureInitializedAsync();
        var response = await _s3Client!.ListBucketsAsync();
        return response.Buckets;
    }
    
    /// <summary>
    /// List objects in a bucket
    /// </summary>
    public async Task<List<S3Object>> ListObjectsAsync(string bucketName, string? prefix = null, int maxKeys = 1000)
    {
        await EnsureInitializedAsync();
    
        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            MaxKeys = maxKeys
        };
    
        if (!string.IsNullOrEmpty(prefix))
        {
            request.Prefix = prefix;
        }
    
        var response = await _s3Client!.ListObjectsV2Async(request);
        return response.S3Objects ?? [];
    }
    
    /// <summary>
    /// Get object content as string
    /// </summary>
    public async Task<string> GetObjectContentAsync(string bucketName, string key)
    {
        await EnsureInitializedAsync();
        
        var response = await _s3Client!.GetObjectAsync(bucketName, key);
        using var reader = new StreamReader(response.ResponseStream);
        return await reader.ReadToEndAsync();
    }
    
    /// <summary>
    /// Get object metadata
    /// </summary>
    public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key)
    {
        await EnsureInitializedAsync();
        return await _s3Client!.GetObjectMetadataAsync(bucketName, key);
    }
    
    /// <summary>
    /// Put object content
    /// </summary>
    public async Task<PutObjectResponse> PutObjectAsync(string bucketName, string key, string content, string? contentType = null)
    {
        await EnsureInitializedAsync();
        
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            ContentBody = content
        };
        
        if (!string.IsNullOrEmpty(contentType))
        {
            request.ContentType = contentType;
        }
        
        return await _s3Client!.PutObjectAsync(request);
    }
    
    /// <summary>
    /// Put object from stream
    /// </summary>
    public async Task<PutObjectResponse> PutObjectAsync(string bucketName, string key, Stream inputStream, string? contentType = null)
    {
        await EnsureInitializedAsync();
        
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = inputStream
        };
        
        if (!string.IsNullOrEmpty(contentType))
        {
            request.ContentType = contentType;
        }
        
        return await _s3Client!.PutObjectAsync(request);
    }
    
    /// <summary>
    /// Delete object
    /// </summary>
    public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key)
    {
        await EnsureInitializedAsync();
        return await _s3Client!.DeleteObjectAsync(bucketName, key);
    }
    
    /// <summary>
    /// Create bucket
    /// </summary>
    public async Task<PutBucketResponse> CreateBucketAsync(string bucketName)
    {
        await EnsureInitializedAsync();
        return await _s3Client!.PutBucketAsync(bucketName);
    }
    
    /// <summary>
    /// Delete bucket
    /// </summary>
    public async Task<DeleteBucketResponse> DeleteBucketAsync(string bucketName)
    {
        await EnsureInitializedAsync();
        return await _s3Client!.DeleteBucketAsync(bucketName);
    }
    
    /// <summary>
    /// Generate presigned URL for object
    /// </summary>
    public async Task<string> GeneratePresignedUrl(string bucketName, string key, DateTime expiry, HttpVerb verb = HttpVerb.GET)
    {
        await EnsureInitializedAsync();
        
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Expires = expiry,
            Verb = verb
        };
        
        return await _s3Client!.GetPreSignedURLAsync(request);
    }
    
    /// <summary>
    /// Check if bucket exists
    /// </summary>
    public async Task<bool> BucketExistsAsync(string bucketName)
    {
        await EnsureInitializedAsync();
        return await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client!, bucketName);
    }
    
    /// <summary>
    /// Check if object exists
    /// </summary>
    public async Task<bool> ObjectExistsAsync(string bucketName, string key)
    {
        await EnsureInitializedAsync();
        
        try
        {
            await _s3Client!.GetObjectMetadataAsync(bucketName, key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
    
    public async Task<GetBucketVersioningResponse> GetBucketVersioningAsync(string bucketName)
    {
        await EnsureInitializedAsync();
        var request = new GetBucketVersioningRequest { BucketName = bucketName };
        return await _s3Client!.GetBucketVersioningAsync(request);
    }
    
    public async Task<ListVersionsResponse> ListObjectVersionsAsync(string bucketName, string? prefix = null)
    {
        await EnsureInitializedAsync();
        var request = new ListVersionsRequest 
        { 
            BucketName = bucketName,
            Prefix = prefix
        };
        return await _s3Client!.ListVersionsAsync(request);
    }
    
    /// <summary>
    /// Auto-initialize with discovered credentials
    /// </summary>
    private async Task AutoInitializeAsync()
    {
        try
        {
            if (await _discoveryService.AutoInitializeAsync())
            {
                var accountInfo = await _discoveryService.GetAccountInfoAsync();
                
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
        if (!_isInitialized && _s3Client == null)
        {
            // Wait up to 5 seconds for auto-initialization
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!_isInitialized && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
            
            if (_s3Client == null)
            {
                throw new InvalidOperationException(
                    "S3 client could not be auto-initialized. Please ensure AWS credentials are configured properly or call InitializeAsync explicitly.");
            }
        }
    }
    
    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
