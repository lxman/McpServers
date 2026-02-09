using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using AzureServer.Core.Services.Storage;
using AzureServer.Core.Services.Storage.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Storage operations
/// </summary>
[McpServerToolType]
public class StorageTools(
    IStorageService storageService,
    ILogger<StorageTools> logger)
{
    #region Storage Account Operations

    [McpServerTool, DisplayName("list_storage_accounts")]
    [Description("List Azure storage accounts. See skills/azure/storage/list-accounts.md only when using this tool")]
    public async Task<string> ListStorageAccounts(string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing storage accounts");
            IEnumerable<StorageAccountDto> accounts = await storageService.ListStorageAccountsAsync(subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                accountCount = accounts.Count(),
                accounts = accounts.Select(a => new
                {
                    name = a.Name,
                    resourceGroup = a.ResourceGroup,
                    location = a.Location,
                    sku = a.Sku,
                    kind = a.Kind,
                    creationTime = a.CreationTime
                })
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing storage accounts");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_storage_account")]
    [Description("Get storage account details. See skills/azure/storage/get-account.md only when using this tool")]
    public async Task<string> GetStorageAccount(
        string subscriptionId,
        string resourceGroupName,
        string accountName)
    {
        try
        {
            logger.LogDebug("Getting storage account {AccountName}", accountName);
            StorageAccountDto? account = await storageService.GetStorageAccountAsync(subscriptionId, resourceGroupName, accountName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                account = new
                {
                    name = account.Name,
                    resourceGroup = account.ResourceGroup,
                    location = account.Location,
                    sku = account.Sku,
                    kind = account.Kind,
                    creationTime = account.CreationTime,
                    primaryLocation = account.PrimaryLocation,
                    secondaryLocation = account.SecondaryLocation
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting storage account {AccountName}", accountName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Container Operations

    [McpServerTool, DisplayName("list_containers")]
    [Description("List blob containers. See skills/azure/storage/list-containers.md only when using this tool")]
    public async Task<string> ListContainers(string accountName, string? prefix = null)
    {
        try
        {
            logger.LogDebug("Listing containers in {AccountName}", accountName);
            IEnumerable<BlobContainerDto> containers = await storageService.ListContainersAsync(accountName, prefix);

            return JsonSerializer.Serialize(new
            {
                success = true,
                containerCount = containers.Count(),
                containers = containers.Select(c => new
                {
                    name = c.Name,
                    lastModified = c.LastModified,
                    publicAccess = c.PublicAccess,
                    leaseState = c.LeaseState,
                    leaseStatus = c.LeaseStatus
                }).ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing containers");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_container")]
    [Description("Get container properties. See skills/azure/storage/get-container.md only when using this tool")]
    public async Task<string> GetContainer(string accountName, string containerName)
    {
        try
        {
            logger.LogDebug("Getting container {ContainerName} in {AccountName}", containerName, accountName);
            BlobContainerDto? container = await storageService.GetContainerAsync(accountName, containerName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                container = new
                {
                    name = container.Name,
                    lastModified = container.LastModified,
                    publicAccess = container.PublicAccess,
                    leaseState = container.LeaseState,
                    leaseStatus = container.LeaseStatus,
                    metadata = container.Metadata
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting container {ContainerName}", containerName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("create_container")]
    [Description("Create blob container. See skills/azure/storage/create-container.md only when using this tool")]
    public async Task<string> CreateContainer(
        string accountName,
        string containerName,
        string publicAccess = "None")
    {
        try
        {
            logger.LogDebug("Creating container {ContainerName} in {AccountName}", containerName, accountName);
            await storageService.CreateContainerAsync(accountName, containerName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Container created successfully",
                containerName,
                accountName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating container {ContainerName}", containerName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("delete_container")]
    [Description("Delete blob container. See skills/azure/storage/delete-container.md only when using this tool")]
    public async Task<string> DeleteContainer(string accountName, string containerName)
    {
        try
        {
            logger.LogDebug("Deleting container {ContainerName} in {AccountName}", containerName, accountName);
            await storageService.DeleteContainerAsync(accountName, containerName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Container deleted successfully",
                containerName,
                accountName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting container {ContainerName}", containerName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Blob Operations

    [McpServerTool, DisplayName("list_blobs")]
    [Description("List blobs in container. See skills/azure/storage/list-blobs.md only when using this tool")]
    public async Task<string> ListBlobs(
        string accountName,
        string containerName,
        string? prefix = null,
        int? maxResults = null)
    {
        try
        {
            logger.LogDebug("Listing blobs in container {ContainerName}", containerName);
            IEnumerable<BlobItemDto> blobs = await storageService.ListBlobsAsync(accountName, containerName, prefix, maxResults);

            return JsonSerializer.Serialize(new
            {
                success = true,
                blobCount = blobs.Count(),
                blobs = blobs.Select(b => new
                {
                    name = b.Name,
                    contentType = b.ContentType,
                    contentLength = b.ContentLength,
                    lastModified = b.LastModified,
                    blobType = b.BlobType,
                    leaseState = b.LeaseState
                }).ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing blobs in container {ContainerName}", containerName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_blob_properties")]
    [Description("Get blob properties. See skills/azure/storage/get-blob-properties.md only when using this tool")]
    public async Task<string> GetBlobProperties(
        string accountName,
        string containerName,
        string blobName)
    {
        try
        {
            logger.LogDebug("Getting properties for blob {BlobName}", blobName);
            BlobPropertiesDto? properties = await storageService.GetBlobPropertiesAsync(accountName, containerName, blobName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                properties = new
                {
                    contentType = properties.ContentType,
                    contentLength = properties.ContentLength,
                    lastModified = properties.LastModified,
                    etag = properties.ETag,
                    blobType = properties.BlobType,
                    leaseState = properties.LeaseState,
                    leaseStatus = properties.LeaseStatus,
                    metadata = properties.Metadata
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting blob properties for {BlobName}", blobName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("download_blob_text")]
    [Description("Download blob as text. See skills/azure/storage/download-blob.md only when using this tool")]
    public async Task<string> DownloadBlobAsText(
        string accountName,
        string containerName,
        string blobName)
    {
        try
        {
            logger.LogDebug("Downloading blob {BlobName} as text", blobName);
            string content = await storageService.DownloadBlobAsTextAsync(accountName, containerName, blobName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                blobName,
                containerName,
                content
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading blob {BlobName}", blobName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("upload_blob_text")]
    [Description("Upload text to blob. See skills/azure/storage/upload-blob.md only when using this tool")]
    public async Task<string> UploadBlobFromText(
        string accountName,
        string containerName,
        string blobName,
        string content,
        string? contentType = null)
    {
        try
        {
            logger.LogDebug("Uploading text to blob {BlobName}", blobName);
            await storageService.UploadBlobFromTextAsync(accountName, containerName, blobName, content, contentType);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Blob uploaded successfully",
                blobName,
                containerName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading blob {BlobName}", blobName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("delete_blob")]
    [Description("Delete blob. See skills/azure/storage/delete-blob.md only when using this tool")]
    public async Task<string> DeleteBlob(
        string accountName,
        string containerName,
        string blobName)
    {
        try
        {
            logger.LogDebug("Deleting blob {BlobName}", blobName);
            await storageService.DeleteBlobAsync(accountName, containerName, blobName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Blob deleted successfully",
                blobName,
                containerName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting blob {BlobName}", blobName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("copy_blob")]
    [Description("Copy blob. See skills/azure/storage/copy-blob.md only when using this tool")]
    public async Task<string> CopyBlob(
        string sourceAccountName,
        string sourceContainerName,
        string sourceBlobName,
        string destAccountName,
        string destContainerName,
        string destBlobName)
    {
        try
        {
            logger.LogDebug("Copying blob from {SourceBlob} to {DestBlob}", sourceBlobName, destBlobName);
            await storageService.CopyBlobAsync(
                sourceAccountName, sourceContainerName, sourceBlobName,
                destAccountName, destContainerName, destBlobName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Blob copied successfully",
                source = new { accountName = sourceAccountName, containerName = sourceContainerName, blobName = sourceBlobName },
                destination = new { accountName = destAccountName, containerName = destContainerName, blobName = destBlobName }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error copying blob");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Metadata Operations

    [McpServerTool, DisplayName("get_blob_metadata")]
    [Description("Get blob metadata. See skills/azure/storage/get-blob-metadata.md only when using this tool")]
    public async Task<string> GetBlobMetadata(
        string accountName,
        string containerName,
        string blobName)
    {
        try
        {
            logger.LogDebug("Getting metadata for blob {BlobName}", blobName);
            Dictionary<string, string> metadata = await storageService.GetBlobMetadataAsync(accountName, containerName, blobName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                blobName,
                metadata
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting blob metadata for {BlobName}", blobName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("set_blob_metadata")]
    [Description("Set blob metadata. See skills/azure/storage/set-blob-metadata.md only when using this tool")]
    public async Task<string> SetBlobMetadata(
        string accountName,
        string containerName,
        string blobName,
        Dictionary<string, string> metadata)
    {
        try
        {
            logger.LogDebug("Setting metadata for blob {BlobName}", blobName);
            await storageService.SetBlobMetadataAsync(accountName, containerName, blobName, metadata);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Metadata updated successfully",
                blobName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting blob metadata for {BlobName}", blobName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region SAS Token Operations

    [McpServerTool, DisplayName("generate_blob_sas")]
    [Description("Generate blob SAS URL. See skills/azure/storage/blob-sas.md only when using this tool")]
    public async Task<string> GenerateBlobSasUrl(
        string accountName,
        string containerName,
        string blobName,
        int expiryHours = 1,
        string permissions = "r")
    {
        try
        {
            logger.LogDebug("Generating SAS URL for blob {BlobName}", blobName);
            SasTokenDto sasUrl = await storageService.GenerateBlobSasUrlAsync(
                accountName, containerName, blobName, expiryHours, permissions);

            return JsonSerializer.Serialize(new
            {
                success = true,
                sasUrl,
                expiresIn = $"{expiryHours} hours",
                permissions
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for blob {BlobName}", blobName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("generate_container_sas")]
    [Description("Generate container SAS URL. See skills/azure/storage/container-sas.md only when using this tool")]
    public async Task<string> GenerateContainerSasUrl(
        string accountName,
        string containerName,
        int expiryHours = 1,
        string permissions = "rl")
    {
        try
        {
            logger.LogDebug("Generating SAS URL for container {ContainerName}", containerName);
            SasTokenDto sasUrl = await storageService.GenerateContainerSasUrlAsync(
                accountName, containerName, expiryHours, permissions);

            return JsonSerializer.Serialize(new
            {
                success = true,
                sasUrl,
                expiresIn = $"{expiryHours} hours",
                permissions
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for container {ContainerName}", containerName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion
}