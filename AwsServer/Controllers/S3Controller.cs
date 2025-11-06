using Amazon.S3;
using Amazon.S3.Model;
using AwsServer.Core.Configuration;
using AwsServer.Core.Services.S3;
using AwsServer.Core.Services.S3.Models;
using Microsoft.AspNetCore.Mvc;
using PutObjectRequest = AwsServer.Core.Models.Requests.PutObjectRequest;

namespace AwsServer.Controllers;

[ApiController]
[Route("api/s3")]
public class S3Controller(S3Service s3Service) : ControllerBase
{
    /// <summary>
    /// Initialize S3 service with AWS credentials and configuration
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] AwsConfiguration config)
    {
        try
        {
            bool success = await s3Service.InitializeAsync(config);
            return Ok(new { success, message = success ? "S3 service initialized successfully" : "Failed to initialize S3 service" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all S3 buckets in the account
    /// </summary>
    [HttpGet("buckets")]
    public async Task<IActionResult> ListBuckets()
    {
        try
        {
            List<S3Bucket> buckets = await s3Service.ListBucketsAsync();
            return Ok(new { success = true, bucketCount = buckets.Count, buckets });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List objects in an S3 bucket with pagination support
    /// </summary>
    [HttpGet("buckets/{bucketName}/objects")]
    public async Task<IActionResult> ListObjects(
        string bucketName,
        [FromQuery] string? prefix = null,
        [FromQuery] int maxKeys = 1000,
        [FromQuery] string? continuationToken = null)
    {
        try
        {
            ListObjectsResult result = await s3Service.ListObjectsAsync(bucketName, prefix, maxKeys, continuationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get the content of an S3 object as text
    /// </summary>
    [HttpGet("buckets/{bucketName}/object-content")]

    public async Task<IActionResult> GetObjectContent(string bucketName, [FromQuery] string key)
    {
        try
        {
            string content = await s3Service.GetObjectContentAsync(bucketName, key);
            return Ok(new { success = true, bucketName, key, content });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>

    /// Get metadata for an S3 object

    /// </summary>

    [HttpGet("buckets/{bucketName}/objects/metadata")]

    public async Task<IActionResult> GetObjectMetadata(string bucketName, [FromQuery] string key)
    {
        try
        {
            GetObjectMetadataResponse metadata = await s3Service.GetObjectMetadataAsync(bucketName, key);
            return Ok(new
            {
                success = true,
                bucketName,
                key,
                contentLength = metadata.ContentLength,
                contentType = metadata.Headers.ContentType,
                lastModified = metadata.LastModified,
                etag = metadata.ETag
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Upload text content to an S3 object
    /// </summary>
    [HttpPut("buckets/{bucketName}/objects/{*key}")]
    public async Task<IActionResult> PutObjectContent(
        string bucketName,
        string key,
        [FromBody] PutObjectRequest request)
    {
        try
        {
            PutObjectResponse response = await s3Service.PutObjectAsync(bucketName, key, request.Content, request.ContentType);
            return Ok(new { success = true, bucketName, key, etag = response.ETag });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an object from an S3 bucket
    /// </summary>
    [HttpDelete("buckets/{bucketName}/objects/{*key}")]
    public async Task<IActionResult> DeleteObject(string bucketName, string key)
    {
        try
        {
            await s3Service.DeleteObjectAsync(bucketName, key);
            return Ok(new { success = true, message = "Object deleted successfully", bucketName, key });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new S3 bucket
    /// </summary>
    [HttpPost("buckets/{bucketName}")]
    public async Task<IActionResult> CreateBucket(string bucketName)
    {
        try
        {
            await s3Service.CreateBucketAsync(bucketName);
            return Ok(new { success = true, message = "Bucket created successfully", bucketName });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an S3 bucket (bucket must be empty)
    /// </summary>
    [HttpDelete("buckets/{bucketName}")]
    public async Task<IActionResult> DeleteBucket(string bucketName)
    {
        try
        {
            await s3Service.DeleteBucketAsync(bucketName);
            return Ok(new { success = true, message = "Bucket deleted successfully", bucketName });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate a presigned URL for temporary access to an S3 object
    /// </summary>
    [HttpPost("buckets/{bucketName}/objects/presigned-url")]
    public async Task<IActionResult> GeneratePresignedUrl(
        string bucketName,
        [FromQuery] string key,
        [FromQuery] int expirationHours = 1,
        [FromQuery] string httpMethod = "GET")
    {
        try
        {
            DateTime expiry = DateTime.UtcNow.AddHours(expirationHours);
            HttpVerb httpVerb = httpMethod.ToUpperInvariant() switch
            {
                "GET" => HttpVerb.GET,
                "PUT" => HttpVerb.PUT,
                "DELETE" => HttpVerb.DELETE,
                _ => HttpVerb.GET
            };

            string url = await s3Service.GeneratePresignedUrl(bucketName, key, expiry, httpVerb);
            return Ok(new { success = true, bucketName, key, presignedUrl = url, expiresAt = expiry });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check if a bucket exists
    /// </summary>
    [HttpGet("buckets/{bucketName}/exists")]
    public async Task<IActionResult> BucketExists(string bucketName)
    {
        try
        {
            bool exists = await s3Service.BucketExistsAsync(bucketName);
            return Ok(new { success = true, bucketName, exists });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check if an object exists in a bucket
    /// </summary>
    [HttpGet("buckets/{bucketName}/objects/exists")]

    public async Task<IActionResult> ObjectExists(string bucketName, [FromQuery] string key)
    {
        try
        {
            bool exists = await s3Service.ObjectExistsAsync(bucketName, key);
            return Ok(new { success = true, bucketName, key, exists });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check if versioning is enabled on an S3 bucket
    /// </summary>
    [HttpGet("buckets/{bucketName}/versioning")]
    public async Task<IActionResult> GetBucketVersioning(string bucketName)
    {
        try
        {
            GetBucketVersioningResponse response = await s3Service.GetBucketVersioningAsync(bucketName);
            return Ok(new
            {
                success = true,
                bucketName,
                versioningEnabled = response.VersioningConfig.Status == VersionStatus.Enabled,
                status = response.VersioningConfig.Status?.Value
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all versions of objects in an S3 bucket
    /// </summary>
    [HttpGet("buckets/{bucketName}/versions")]
    public async Task<IActionResult> ListObjectVersions(string bucketName, [FromQuery] string? prefix = null)
    {
        try
        {
            ListVersionsResponse response = await s3Service.ListObjectVersionsAsync(bucketName, prefix);
            return Ok(new
            {
                success = true,
                bucketName,
                prefix,
                versionCount = response.Versions.Count,
                versions = response.Versions
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

