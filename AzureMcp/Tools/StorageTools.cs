using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.Storage;
using AzureMcp.Services.Storage.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class StorageTools(IStorageService storageService)
{
    #region Storage Account Tools

    [McpServerTool]
    [Description("List all storage accounts, optionally filtered by subscription")]
    public async Task<string> ListStorageAccountsAsync(
        [Description("Optional subscription ID to filter storage accounts")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<StorageAccountDto> accounts = await storageService.ListStorageAccountsAsync(subscriptionId);
            return JsonSerializer.Serialize(new { success = true, storageAccounts = accounts.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListStorageAccounts");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific storage account")]
    public async Task<string> GetStorageAccountAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Storage account name")] string accountName)
    {
        try
        {
            StorageAccountDto? account = await storageService.GetStorageAccountAsync(subscriptionId, resourceGroupName, accountName);
            if (account == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Storage account {accountName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, storageAccount = account },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetStorageAccount");
        }
    }

    #endregion

    #region Container Tools

    [McpServerTool]
    [Description("List containers in a storage account")]
    public async Task<string> ListContainersAsync(
        [Description("Storage account name")] string accountName,
        [Description("Optional prefix to filter containers")] string? prefix = null)
    {
        try
        {
            IEnumerable<BlobContainerDto> containers = await storageService.ListContainersAsync(accountName, prefix);
            return JsonSerializer.Serialize(new { success = true, containers = containers.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListContainers");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific container")]
    public async Task<string> GetContainerAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName)
    {
        try
        {
            BlobContainerDto? container = await storageService.GetContainerAsync(accountName, containerName);
            if (container == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Container {containerName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, container },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetContainer");
        }
    }

    [McpServerTool]
    [Description("Create a new container in a storage account")]
    public async Task<string> CreateContainerAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Optional metadata as JSON object (e.g., '{\"key\":\"value\"}')")] string? metadataJson = null)
    {
        try
        {
            Dictionary<string, string>? metadata = null;
            if (!string.IsNullOrEmpty(metadataJson))
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
            }

            BlobContainerDto container = await storageService.CreateContainerAsync(accountName, containerName, metadata);
            return JsonSerializer.Serialize(new { success = true, container },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateContainer");
        }
    }

    [McpServerTool]
    [Description("Delete a container from a storage account")]
    public async Task<string> DeleteContainerAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName)
    {
        try
        {
            bool deleted = await storageService.DeleteContainerAsync(accountName, containerName);
            return JsonSerializer.Serialize(new
            {
                success = true,
                deleted,
                message = deleted ? "Container deleted successfully" : "Container not found"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteContainer");
        }
    }

    [McpServerTool]
    [Description("Check if a container exists in a storage account")]
    public async Task<string> ContainerExistsAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName)
    {
        try
        {
            bool exists = await storageService.ContainerExistsAsync(accountName, containerName);
            return JsonSerializer.Serialize(new { success = true, exists },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ContainerExists");
        }
    }

    #endregion

    #region Blob Tools

    [McpServerTool]
    [Description("List blobs in a container")]
    public async Task<string> ListBlobsAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Optional prefix to filter blobs")] string? prefix = null,
        [Description("Optional maximum number of blobs to return")] int? maxResults = null)
    {
        try
        {
            IEnumerable<BlobItemDto> blobs = await storageService.ListBlobsAsync(accountName, containerName, prefix, maxResults);
            return JsonSerializer.Serialize(new { success = true, blobs = blobs.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListBlobs");
        }
    }

    [McpServerTool]
    [Description("Get properties and metadata of a specific blob")]
    public async Task<string> GetBlobPropertiesAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName)
    {
        try
        {
            BlobPropertiesDto? properties = await storageService.GetBlobPropertiesAsync(accountName, containerName, blobName);
            if (properties == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Blob {blobName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, blobProperties = properties },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBlobProperties");
        }
    }

    [McpServerTool]
    [Description("Download a blob as text content")]
    public async Task<string> DownloadBlobAsTextAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName)
    {
        try
        {
            string content = await storageService.DownloadBlobAsTextAsync(accountName, containerName, blobName);
            return JsonSerializer.Serialize(new { success = true, content, blobName, containerName, accountName },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DownloadBlobAsText");
        }
    }

    [McpServerTool]
    [Description("Upload text content as a blob")]
    public async Task<string> UploadBlobFromTextAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName,
        [Description("Text content to upload")] string content,
        [Description("Optional content type (default: text/plain)")] string? contentType = null)
    {
        try
        {
            BlobItemDto blob = await storageService.UploadBlobFromTextAsync(accountName, containerName, blobName, content, contentType);
            return JsonSerializer.Serialize(new { success = true, blob, message = "Blob uploaded successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UploadBlobFromText");
        }
    }

    [McpServerTool]
    [Description("Delete a blob from a container")]
    public async Task<string> DeleteBlobAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName)
    {
        try
        {
            bool deleted = await storageService.DeleteBlobAsync(accountName, containerName, blobName);
            return JsonSerializer.Serialize(new
            {
                success = true,
                deleted,
                message = deleted ? "Blob deleted successfully" : "Blob not found"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteBlob");
        }
    }

    [McpServerTool]
    [Description("Check if a blob exists in a container")]
    public async Task<string> BlobExistsAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName)
    {
        try
        {
            bool exists = await storageService.BlobExistsAsync(accountName, containerName, blobName);
            return JsonSerializer.Serialize(new { success = true, exists },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "BlobExists");
        }
    }

    [McpServerTool]
    [Description("Copy a blob from one location to another (can be same or different account/container)")]
    public async Task<string> CopyBlobAsync(
        [Description("Source storage account name")] string sourceAccountName,
        [Description("Source container name")] string sourceContainerName,
        [Description("Source blob name")] string sourceBlobName,
        [Description("Destination storage account name")] string destAccountName,
        [Description("Destination container name")] string destContainerName,
        [Description("Destination blob name")] string destBlobName)
    {
        try
        {
            BlobItemDto blob = await storageService.CopyBlobAsync(
                sourceAccountName, sourceContainerName, sourceBlobName,
                destAccountName, destContainerName, destBlobName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                blob,
                message = "Blob copied successfully",
                source = $"{sourceAccountName}/{sourceContainerName}/{sourceBlobName}",
                destination = $"{destAccountName}/{destContainerName}/{destBlobName}"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CopyBlob");
        }
    }

    #endregion

    #region Blob Metadata Tools

    [McpServerTool]
    [Description("Get metadata for a blob")]
    public async Task<string> GetBlobMetadataAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName)
    {
        try
        {
            Dictionary<string, string> metadata = await storageService.GetBlobMetadataAsync(accountName, containerName, blobName);
            return JsonSerializer.Serialize(new { success = true, metadata },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBlobMetadata");
        }
    }

    [McpServerTool]
    [Description("Set metadata for a blob")]
    public async Task<string> SetBlobMetadataAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName,
        [Description("Metadata as JSON object (e.g., '{\"key\":\"value\"}')")] string metadataJson)
    {
        try
        {
            Dictionary<string, string> metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson)
                ?? throw new ArgumentException("Invalid metadata JSON");

            await storageService.SetBlobMetadataAsync(accountName, containerName, blobName, metadata);
            return JsonSerializer.Serialize(new { success = true, message = "Metadata set successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SetBlobMetadata");
        }
    }

    #endregion

    #region SAS Token Tools

    [McpServerTool]
    [Description("Generate a temporary SAS (Shared Access Signature) URL for a blob with read access")]
    public async Task<string> GenerateBlobSasUrlAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Blob name")] string blobName,
        [Description("Expiration time in hours (default: 1)")] int expirationHours = 1,
        [Description("Permissions: r=read, w=write, d=delete, a=add, c=create (default: r)")] string permissions = "r")
    {
        try
        {
            SasTokenDto sasToken = await storageService.GenerateBlobSasUrlAsync(
                accountName, containerName, blobName, expirationHours, permissions);

            return JsonSerializer.Serialize(new { success = true, sasToken },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GenerateBlobSasUrl");
        }
    }

    [McpServerTool]
    [Description("Generate a temporary SAS (Shared Access Signature) URL for a container with list and read access")]
    public async Task<string> GenerateContainerSasUrlAsync(
        [Description("Storage account name")] string accountName,
        [Description("Container name")] string containerName,
        [Description("Expiration time in hours (default: 1)")] int expirationHours = 1,
        [Description("Permissions: r=read, w=write, d=delete, l=list, a=add, c=create (default: rl)")] string permissions = "rl")
    {
        try
        {
            SasTokenDto sasToken = await storageService.GenerateContainerSasUrlAsync(
                accountName, containerName, expirationHours, permissions);

            return JsonSerializer.Serialize(new { success = true, sasToken },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GenerateContainerSasUrl");
        }
    }

    #endregion

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = ex.Message,
            operation,
            type = ex.GetType().Name,
            stackTrace = ex.StackTrace
        }, SerializerOptions.JsonOptionsIndented);
    }
}