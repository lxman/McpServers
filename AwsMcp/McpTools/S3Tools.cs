using System.ComponentModel;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using AwsServer.Core.Services.S3;
using AwsServer.Core.Services.S3.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AwsMcp.McpTools;

/// <summary>
/// MCP tools for AWS S3 operations
/// </summary>
[McpServerToolType]
public class S3Tools(
    S3Service s3Service,
    ILogger<S3Tools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_s3_buckets")]
    [Description("List S3 buckets. See skills/aws/s3/list-buckets.md only when using this tool")]
    public async Task<string> ListS3Buckets()
    {
        try
        {
            logger.LogDebug("Listing S3 buckets");
            var buckets = await s3Service.ListBucketsAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketCount = buckets.Count,
                buckets = buckets.Select(b => new
                {
                    name = b.BucketName,
                    creationDate = b.CreationDate
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing S3 buckets");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_s3_objects")]
    [Description("List objects in S3 bucket. See skills/aws/s3/list-objects.md only when using this tool")]
    public async Task<string> ListS3Objects(
        string bucketName,
        string? prefix = null,
        int maxKeys = 1000,
        string? continuationToken = null)
    {
        try
        {
            logger.LogDebug("Listing objects in bucket {BucketName} with prefix {Prefix}", bucketName, prefix);
            var result = await s3Service.ListObjectsAsync(bucketName, prefix, maxKeys, continuationToken);

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing objects in bucket {BucketName}", bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_s3_object")]
    [Description("Get S3 object content. See skills/aws/s3/get-object.md only when using this tool")]
    public async Task<string> GetS3Object(
        string bucketName,
        string key)
    {
        try
        {
            logger.LogDebug("Getting object {Key} from bucket {BucketName}", key, bucketName);
            var content = await s3Service.GetObjectContentAsync(bucketName, key);

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                content
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting object {Key} from bucket {BucketName}", key, bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("put_s3_object")]
    [Description("Put object to S3 bucket. See skills/aws/s3/put-object.md only when using this tool")]
    public async Task<string> PutS3Object(
        string bucketName,
        string key,
        string content,
        string contentType = "text/plain")
    {
        try
        {
            logger.LogDebug("Putting object {Key} to bucket {BucketName}", key, bucketName);
            var response = await s3Service.PutObjectAsync(bucketName, key, content, contentType);

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                etag = response.ETag
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error putting object {Key} to bucket {BucketName}", key, bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_s3_object")]
    [Description("Delete S3 object. See skills/aws/s3/delete-object.md only when using this tool")]
    public async Task<string> DeleteS3Object(
        string bucketName,
        string key)
    {
        try
        {
            logger.LogDebug("Deleting object {Key} from bucket {BucketName}", key, bucketName);
            await s3Service.DeleteObjectAsync(bucketName, key);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Object deleted successfully",
                bucketName,
                key
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting object {Key} from bucket {BucketName}", key, bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_s3_bucket")]
    [Description("Create S3 bucket. See skills/aws/s3/create-bucket.md only when using this tool")]
    public async Task<string> CreateS3Bucket(
        string bucketName)
    {
        try
        {
            logger.LogDebug("Creating S3 bucket {BucketName}", bucketName);
            await s3Service.CreateBucketAsync(bucketName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Bucket created successfully",
                bucketName
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating S3 bucket {BucketName}", bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("generate_presigned_url")]
    [Description("Generate presigned URL for S3 object. See skills/aws/s3/presigned-url.md only when using this tool")]
    public async Task<string> GeneratePresignedUrl(
        string bucketName,
        string key,
        int expirationHours = 1,
        string httpMethod = "GET")
    {
        try
        {
            logger.LogDebug("Generating presigned URL for {Key} in bucket {BucketName}", key, bucketName);
            var expiry = DateTime.UtcNow.AddHours(expirationHours);
            var httpVerb = httpMethod.ToUpperInvariant() switch
            {
                "GET" => HttpVerb.GET,
                "PUT" => HttpVerb.PUT,
                "DELETE" => HttpVerb.DELETE,
                _ => HttpVerb.GET
            };

            var url = await s3Service.GeneratePresignedUrl(bucketName, key, expiry, httpVerb);

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                presignedUrl = url,
                expiresAt = expiry
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating presigned URL for {Key} in bucket {BucketName}", key, bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_s3_bucket")]
    [Description("Delete S3 bucket. See skills/aws/s3/delete-bucket.md only when using this tool")]
    public async Task<string> DeleteS3Bucket(string bucketName)
    {
        try
        {
            logger.LogDebug("Deleting S3 bucket {BucketName}", bucketName);
            await s3Service.DeleteBucketAsync(bucketName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Bucket deleted successfully",
                bucketName
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting S3 bucket {BucketName}", bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("bucket_exists")]
    [Description("Check if S3 bucket exists. See skills/aws/s3/bucket-exists.md only when using this tool")]
    public async Task<string> BucketExists(string bucketName)
    {
        try
        {
            logger.LogDebug("Checking if S3 bucket {BucketName} exists", bucketName);
            var exists = await s3Service.BucketExistsAsync(bucketName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                exists
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if S3 bucket {BucketName} exists", bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("object_exists")]
    [Description("Check if S3 object exists. See skills/aws/s3/object-exists.md only when using this tool")]
    public async Task<string> ObjectExists(string bucketName, string key)
    {
        try
        {
            logger.LogDebug("Checking if S3 object {Key} exists in bucket {BucketName}", key, bucketName);
            var exists = await s3Service.ObjectExistsAsync(bucketName, key);

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                key,
                exists
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if S3 object {Key} exists in bucket {BucketName}", key, bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_bucket_versioning")]
    [Description("Get S3 bucket versioning. See skills/aws/s3/get-bucket-versioning.md only when using this tool")]
    public async Task<string> GetBucketVersioning(string bucketName)
    {
        try
        {
            logger.LogDebug("Getting versioning for S3 bucket {BucketName}", bucketName);
            var response = await s3Service.GetBucketVersioningAsync(bucketName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                versioningEnabled = response.VersioningConfig.Status == VersionStatus.Enabled,
                status = response.VersioningConfig.Status?.Value
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting versioning for S3 bucket {BucketName}", bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_object_versions")]
    [Description("List S3 object versions. See skills/aws/s3/list-object-versions.md only when using this tool")]
    public async Task<string> ListObjectVersions(string bucketName, string? prefix = null)
    {
        try
        {
            logger.LogDebug("Listing object versions in S3 bucket {BucketName}", bucketName);
            var response = await s3Service.ListObjectVersionsAsync(bucketName, prefix);

            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                prefix,
                versionCount = response.Versions.Count,
                versions = response.Versions.Select(v => new
                {
                    key = v.Key,
                    versionId = v.VersionId,
                    isLatest = v.IsLatest,
                    lastModified = v.LastModified,
                    size = v.Size,
                    etag = v.ETag
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing object versions in S3 bucket {BucketName}", bucketName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}