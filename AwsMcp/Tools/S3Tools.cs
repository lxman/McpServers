using System.ComponentModel;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using ModelContextProtocol.Server;
using AwsMcp.S3;
using AwsMcp.Configuration;

namespace AwsMcp.Tools;

[McpServerToolType]
public class S3Tools
{
    private readonly S3Service _s3Service;

    public S3Tools(S3Service s3Service)
    {
        _s3Service = s3Service;
    }

    [McpServerTool]
    [Description("Initialize S3 service with AWS credentials and configuration")]
    public async Task<string> InitializeS3(
        [Description("AWS region (default: us-east-1)")]
        string region = "us-east-1",
        [Description("AWS Access Key ID (optional if using profile or environment)")]
        string? accessKeyId = null,
        [Description("AWS Secret Access Key (optional if using profile or environment)")]
        string? secretAccessKey = null,
        [Description("AWS Profile name (optional)")]
        string? profileName = null,
        [Description("Custom service URL for LocalStack or other endpoints (optional)")]
        string? serviceUrl = null,
        [Description("Force path style for S3 URLs (useful for LocalStack)")]
        bool forcePathStyle = false)
    {
        try
        {
            var config = new AwsConfiguration
            {
                Region = region,
                AccessKeyId = accessKeyId,
                SecretAccessKey = secretAccessKey,
                ProfileName = profileName,
                ServiceUrl = serviceUrl,
                ForcePathStyle = forcePathStyle
            };

            bool success = await _s3Service.InitializeAsync(config);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "S3 service initialized successfully" : "Failed to initialize S3 service",
                region,
                usingProfile = !string.IsNullOrEmpty(profileName),
                usingCustomEndpoint = !string.IsNullOrEmpty(serviceUrl)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("List all S3 buckets in the account")]
    public async Task<string> ListBuckets()
    {
        try
        {
            List<S3Bucket> buckets = await _s3Service.ListBucketsAsync();
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketCount = buckets.Count,
                buckets = buckets.Select(b => new
                {
                    name = b.BucketName,
                    creationDate = b.CreationDate
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("List objects in an S3 bucket")]
    public async Task<string> ListObjects(
        [Description("Name of the S3 bucket")]
        string bucketName,
        [Description("Prefix to filter objects (optional)")]
        string? prefix = null,
        [Description("Maximum number of objects to return (default: 1000)")]
        int maxKeys = 1000)
    {
        try
        {
            List<S3Object> objects = await _s3Service.ListObjectsAsync(bucketName, prefix, maxKeys);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                prefix,
                objectCount = objects.Count,
                objects = objects.Select(o => new
                {
                    key = o.Key,
                    size = o.Size,
                    lastModified = o.LastModified,
                    storageClass = o.StorageClass?.Value,
                    etag = o.ETag
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Get the content of an S3 object as text")]
    public async Task<string> GetObjectContent(
        [Description("Name of the S3 bucket")]
        string bucketName,
        [Description("Key (path) of the object")]
        string key)
    {
        try
        {
            string content = await _s3Service.GetObjectContentAsync(bucketName, key);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                contentLength = content.Length,
                content
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Get metadata for an S3 object")]
    public async Task<string> GetObjectMetadata(
        [Description("Name of the S3 bucket")]
        string bucketName,
        [Description("Key (path) of the object")]
        string key)
    {
        try
        {
            GetObjectMetadataResponse metadata = await _s3Service.GetObjectMetadataAsync(bucketName, key);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                contentLength = metadata.ContentLength,
                contentType = metadata.Headers.ContentType,
                lastModified = metadata.LastModified,
                etag = metadata.ETag,
                serverSideEncryption = metadata.ServerSideEncryptionMethod?.Value,
                metadata = metadata.Metadata
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Upload text content to an S3 object")]
    public async Task<string> PutObjectContent(
        [Description("Name of the S3 bucket")]
        string bucketName,
        [Description("Key (path) for the object")]
        string key,
        [Description("Text content to upload")]
        string content,
        [Description("Content type (optional, e.g., 'text/plain', 'application/json')")]
        string? contentType = null)
    {
        try
        {
            PutObjectResponse response = await _s3Service.PutObjectAsync(bucketName, key, content, contentType);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                etag = response.ETag,
                contentLength = content.Length,
                contentType
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Delete an object from an S3 bucket")]
    public async Task<string> DeleteObject(
        [Description("Name of the S3 bucket")]
        string bucketName,
        [Description("Key (path) of the object to delete")]
        string key)
    {
        try
        {
            await _s3Service.DeleteObjectAsync(bucketName, key);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Object deleted successfully",
                bucketName,
                key
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Create a new S3 bucket")]
    public async Task<string> CreateBucket(
        [Description("Name of the bucket to create")]
        string bucketName)
    {
        try
        {
            await _s3Service.CreateBucketAsync(bucketName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Bucket created successfully",
                bucketName
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Delete an S3 bucket (bucket must be empty)")]
    public async Task<string> DeleteBucket(
        [Description("Name of the bucket to delete")]
        string bucketName)
    {
        try
        {
            await _s3Service.DeleteBucketAsync(bucketName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Bucket deleted successfully",
                bucketName
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Generate a presigned URL for temporary access to an S3 object")]
    public string GeneratePresignedUrl(
        [Description("Name of the S3 bucket")]
        string bucketName,
        [Description("Key (path) of the object")]
        string key,
        [Description("Expiration time in hours from now (default: 1 hour)")]
        int expirationHours = 1,
        [Description("HTTP method for the URL (GET, PUT, DELETE)")]
        string httpMethod = "GET")
    {
        try
        {
            DateTime expiry = DateTime.UtcNow.AddHours(expirationHours);
            HttpVerb httpVerb = httpMethod.ToUpperInvariant() switch
            {
                "GET" => Amazon.S3.HttpVerb.GET,
                "PUT" => Amazon.S3.HttpVerb.PUT,
                "DELETE" => Amazon.S3.HttpVerb.DELETE,
                _ => Amazon.S3.HttpVerb.GET
            };
            
            string url = _s3Service.GeneratePresignedUrl(bucketName, key, expiry, httpVerb);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                presignedUrl = url,
                httpMethod,
                expiresAt = expiry,
                expirationHours
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Check if a bucket exists")]
    public async Task<string> BucketExists(
        [Description("Name of the bucket to check")]
        string bucketName)
    {
        try
        {
            bool exists = await _s3Service.BucketExistsAsync(bucketName);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                exists
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Check if an object exists in a bucket")]
    public async Task<string> ObjectExists(
        [Description("Name of the S3 bucket")]
        string bucketName,
        [Description("Key (path) of the object to check")]
        string key)
    {
        try
        {
            bool exists = await _s3Service.ObjectExistsAsync(bucketName, key);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                exists
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
