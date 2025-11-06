using AzureServer.Core.Services.Storage.Models;

namespace AzureServer.Core.Services.Storage;

public interface IStorageService
{
    // Storage Account Operations
    Task<IEnumerable<StorageAccountDto>> ListStorageAccountsAsync(string? subscriptionId = null);
    Task<StorageAccountDto?> GetStorageAccountAsync(string subscriptionId, string resourceGroupName, string accountName);
    
    // Container Operations
    Task<IEnumerable<BlobContainerDto>> ListContainersAsync(string accountName, string? prefix = null);
    Task<BlobContainerDto?> GetContainerAsync(string accountName, string containerName);
    Task<BlobContainerDto> CreateContainerAsync(string accountName, string containerName, Dictionary<string, string>? metadata = null);
    Task<bool> DeleteContainerAsync(string accountName, string containerName);
    Task<bool> ContainerExistsAsync(string accountName, string containerName);
    
    // Blob Operations
    Task<IEnumerable<BlobItemDto>> ListBlobsAsync(string accountName, string containerName, string? prefix = null, int? maxResults = null);
    Task<BlobPropertiesDto?> GetBlobPropertiesAsync(string accountName, string containerName, string blobName);
    Task<string> DownloadBlobAsTextAsync(string accountName, string containerName, string blobName);
    Task<byte[]> DownloadBlobAsBytesAsync(string accountName, string containerName, string blobName);
    Task<BlobItemDto> UploadBlobFromTextAsync(string accountName, string containerName, string blobName, string content, string? contentType = null);
    Task<BlobItemDto> UploadBlobFromBytesAsync(string accountName, string containerName, string blobName, byte[] content, string? contentType = null);
    Task<bool> DeleteBlobAsync(string accountName, string containerName, string blobName);
    Task<bool> BlobExistsAsync(string accountName, string containerName, string blobName);
    Task<BlobItemDto> CopyBlobAsync(string sourceAccountName, string sourceContainerName, string sourceBlobName, 
        string destAccountName, string destContainerName, string destBlobName);
    
    // Blob Metadata Operations
    Task<Dictionary<string, string>> GetBlobMetadataAsync(string accountName, string containerName, string blobName);
    Task SetBlobMetadataAsync(string accountName, string containerName, string blobName, Dictionary<string, string> metadata);
    
    // SAS Token Operations
    Task<SasTokenDto> GenerateBlobSasUrlAsync(string accountName, string containerName, string blobName, 
        int expirationHours = 1, string permissions = "r");
    Task<SasTokenDto> GenerateContainerSasUrlAsync(string accountName, string containerName, 
        int expirationHours = 1, string permissions = "rl");
}