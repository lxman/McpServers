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
            return HandleError(ex, "InitializeS3");
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
            return HandleError(ex, "ListBuckets");
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
                    key = o.Key ?? "",
                    size = o.Size ?? 0,
                    lastModified = o.LastModified ?? DateTime.MinValue,
                    storageClass = o.StorageClass?.Value ?? "STANDARD",
                    etag = o.ETag ?? ""
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListObjects");
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
            return HandleError(ex, "GetObjectContent");
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
            return HandleError(ex, "GetObjectMetadata");
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
            return HandleError(ex, "PutObjectContent");
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
            return HandleError(ex, "DeleteObject");
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
            return HandleError(ex, "CreateBucket");
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
            return HandleError(ex, "DeleteBucket");
        }
    }

    [McpServerTool]
    [Description("Generate a presigned URL for temporary access to an S3 object")]
    public async Task<string> GeneratePresignedUrl(
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
                "GET" => HttpVerb.GET,
                "PUT" => HttpVerb.PUT,
                "DELETE" => HttpVerb.DELETE,
                _ => HttpVerb.GET
            };
            
            string url = await _s3Service.GeneratePresignedUrl(bucketName, key, expiry, httpVerb);
            
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
            return HandleError(ex, "GeneratePresignedUrl");
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
            return HandleError(ex, "BucketExists");
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
            return HandleError(ex, "ObjectExists");
        }
    }
    
    [McpServerTool]
    [Description("Check if versioning is enabled on an S3 bucket")]
    public async Task<string> GetBucketVersioning(
        [Description("Name of the S3 bucket")]
        string bucketName)
    {
        try
        {
            GetBucketVersioningResponse response = await _s3Service.GetBucketVersioningAsync(bucketName);
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                versioningEnabled = response.VersioningConfig.Status == VersionStatus.Enabled,
                status = response.VersioningConfig.Status?.Value,
                mfaDelete = response.VersioningConfig.EnableMfaDelete
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "checking S3 bucket versioning");
        }
    }
    
    [McpServerTool]
    [Description("List all versions of objects in an S3 bucket")]
    public async Task<string> ListObjectVersions(string bucketName, string? prefix = null)
    {
        try
        {
            ListVersionsResponse response = await _s3Service.ListObjectVersionsAsync(bucketName, prefix);
            List<S3ObjectVersion> delMarkers = response.Versions.Where(v => v.IsDeleteMarker.HasValue && v.IsDeleteMarker.Value).ToList();
            return JsonSerializer.Serialize(new
            {
                success = true,
                bucketName,
                prefix,
                versionCount = response.Versions.Count,
                deleteMarkerCount = delMarkers.Count,
                versions = response.Versions.Select(v => new
                {
                    key = v.Key,
                    versionId = v.VersionId,
                    isLatest = v.IsLatest,
                    lastModified = v.LastModified,
                    size = v.Size,
                    etag = v.ETag,
                    storageClass = v.StorageClass?.Value
                }).ToList(),
                deleteMarkers = delMarkers.Select(d => new
                {
                    key = d.Key,
                    versionId = d.VersionId,
                    isLatest = d.IsLatest,
                    lastModified = d.LastModified
                }).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        } catch (Exception ex)
        {
            return HandleError(ex, "listing S3 object versions");
        }
    }
    
    /// <summary>
    /// Enhanced error handling for S3 operations with user-friendly messages
    /// </summary>
    private static string HandleError(Exception ex, string operation)
    {
        object error;

        switch (ex)
        {
            case InvalidOperationException invalidOpEx:
                error = new
                {
                    success = false,
                    error = "S3 service not initialized or missing permissions",
                    details = invalidOpEx.Message,
                    suggestedActions = new[]
                    {
                        "Ensure you have called InitializeS3 first",
                        "Check your AWS credentials and permissions",
                        "Verify your AWS region is correct",
                        "For S3, you need s3:ListBucket and/or s3:GetObject permissions"
                    },
                    errorType = "ServiceNotInitialized"
                };
                break;
            case AmazonS3Exception s3Ex:
            {
                string[] actions = s3Ex.ErrorCode switch
                {
                    "NoSuchBucket" => new[]
                    {
                        "The specified bucket does not exist",
                        "Check the bucket name for typos",
                        "Verify you're in the correct AWS region",
                        "Use ListBuckets to see available buckets"
                    },
                    "AccessDenied" => new[]
                    {
                        "You don't have permission to access this S3 resource",
                        "Check your IAM permissions for S3",
                        "Ensure your user/role has s3:ListBucket, s3:GetObject, or s3:PutObject permissions",
                        "Try: aws s3 ls to test S3 access from CLI"
                    },
                    "NoSuchKey" => new[]
                    {
                        "The specified object key does not exist in the bucket",
                        "Check the object key (path) for typos",
                        "Use ListObjects to see available objects in the bucket",
                        "Verify the object hasn't been deleted"
                    },
                    "BucketAlreadyExists" => new[]
                    {
                        "A bucket with this name already exists",
                        "Bucket names must be globally unique across all AWS accounts",
                        "Try a different bucket name",
                        "Add a unique suffix like your account ID or timestamp"
                    },
                    "BucketNotEmpty" => new[]
                    {
                        "Cannot delete bucket because it contains objects",
                        "Delete all objects in the bucket first",
                        "Use ListObjects to see what objects remain",
                        "Consider using aws s3 rm s3://bucket-name --recursive from CLI"
                    },
                    "InvalidBucketName" => new[]
                    {
                        "Bucket name does not meet AWS naming requirements",
                        "Bucket names must be 3-63 characters, lowercase, no special characters",
                        "Cannot contain uppercase letters, spaces, or special characters",
                        "Must start and end with lowercase letter or number"
                    },
                    "NoSuchUpload" => new[]
                    {
                        "The specified multipart upload does not exist",
                        "The upload may have been aborted or completed",
                        "Start a new upload operation"
                    },
                    "InvalidPart" => new[]
                    {
                        "One or more parts of the multipart upload is invalid",
                        "Check part numbers and ETags",
                        "Ensure all parts except the last are at least 5MB"
                    },
                    "PermanentRedirect" => new[]
                    {
                        "Bucket is in a different region than specified",
                        "Check the bucket's actual region",
                        "Update your region configuration to match the bucket's region"
                    },
                    _ => new[]
                    {
                        "Check AWS S3 service status at https://status.aws.amazon.com/",
                        "Verify your parameters and try again",
                        $"AWS S3 Error Code: {s3Ex.ErrorCode} - consult AWS S3 documentation"
                    }
                };

                error = new
                {
                    success = false,
                    error = $"AWS S3 service error: {s3Ex.ErrorCode}",
                    details = s3Ex.Message,
                    suggestedActions = actions,
                    errorType = "AWSS3Service",
                    statusCode = s3Ex.StatusCode.ToString(),
                    awsErrorCode = s3Ex.ErrorCode,
                    requestId = s3Ex.RequestId
                };
                break;
            }
            case Amazon.Runtime.AmazonServiceException awsEx:
            {
                string[] actions = awsEx.ErrorCode switch
                {
                    "InvalidAccessKeyId" => new[]
                    {
                        "The AWS access key ID provided does not exist",
                        "Check your access key ID for typos",
                        "Verify the access key hasn't been deleted or deactivated",
                        "Try: aws sts get-caller-identity to verify your credentials"
                    },
                    "SignatureDoesNotMatch" => new[]
                    {
                        "The AWS secret access key provided is incorrect",
                        "Check your secret access key for typos",
                        "Ensure there are no extra spaces in your credentials",
                        "Verify you're using the correct secret key for your access key ID"
                    },
                    "TokenRefreshRequired" => new[]
                    {
                        "Your AWS session token has expired",
                        "Refresh your credentials or remove the session token",
                        "If using temporary credentials, obtain new ones",
                        "Check if you're using AWS SSO and need to refresh your session"
                    },
                    "UnauthorizedOperation" => new[]
                    {
                        "Your AWS credentials don't have permission for this S3 operation",
                        "Contact your AWS administrator to grant S3 permissions",
                        "Check what S3 permissions your role/user has in IAM"
                    },
                    "RequestTimeTooSkewed" => new[]
                    {
                        "Your system clock is too far off from AWS server time",
                        "Synchronize your system clock",
                        "Check your timezone settings"
                    },
                    _ => new[]
                    {
                        "Check AWS S3 service status at https://status.aws.amazon.com/",
                        "Verify your parameters and try again",
                        $"AWS Error Code: {awsEx.ErrorCode} - consult AWS documentation"
                    }
                };

                error = new
                {
                    success = false,
                    error = $"AWS service error: {awsEx.ErrorCode}",
                    details = awsEx.Message,
                    suggestedActions = actions,
                    errorType = "AWSService",
                    statusCode = awsEx.StatusCode.ToString(),
                    awsErrorCode = awsEx.ErrorCode
                };
                break;
            }
            case Amazon.Runtime.AmazonClientException clientEx:
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
                        "Verify your AWS service URL if using LocalStack",
                        "For S3, ensure the endpoint format is correct (path-style vs virtual-hosted)"
                    },
                    errorType = "NetworkOrConfiguration"
                };
                break;
            case ArgumentException argEx:
                error = new
                {
                    success = false,
                    error = "Invalid parameter provided to S3 operation",
                    details = argEx.Message,
                    suggestedActions = new[]
                    {
                        "Check bucket names meet AWS requirements (3-63 chars, lowercase, no special chars)",
                        "Verify object keys don't contain invalid characters",
                        "Ensure expiration times are positive numbers",
                        "Check that HTTP methods are GET, PUT, or DELETE"
                    },
                    errorType = "InvalidParameter"
                };
                break;
            case FileNotFoundException fileEx:
                error = new
                {
                    success = false,
                    error = "File not found for S3 upload operation",
                    details = fileEx.Message,
                    suggestedActions = new[]
                    {
                        "Verify the file path is correct",
                        "Check that the file exists and is accessible",
                        "Ensure you have read permissions for the file"
                    },
                    errorType = "FileNotFound"
                };
                break;
            case UnauthorizedAccessException accessEx:
                error = new
                {
                    success = false,
                    error = "Insufficient permissions to access local file",
                    details = accessEx.Message,
                    suggestedActions = new[]
                    {
                        "Check file permissions on your local system",
                        "Ensure the file is not locked by another process",
                        "Try running with elevated permissions if necessary"
                    },
                    errorType = "LocalFileAccess"
                };
                break;
            default:
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
                        "For S3 operations, verify bucket and object names are valid",
                        "Contact support if the issue persists",
                        $"Exception type: {ex.GetType().Name}"
                    },
                    errorType = "Unexpected",
                    exceptionType = ex.GetType().Name
                };
                break;
        }

        return JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true });
    }
}
