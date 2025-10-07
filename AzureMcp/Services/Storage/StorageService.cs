using System.Text;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using AzureMcp.Authentication;
using AzureMcp.Services.Core;
using AzureMcp.Services.Storage.Models;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Services.Storage;

public class StorageService(
    ArmClientFactory armClientFactory,
    ILogger<StorageService> logger) : IStorageService
{
    private readonly Dictionary<string, BlobServiceClient> _blobServiceClients = new();

    private async Task<BlobServiceClient> GetBlobServiceClientAsync(string accountName)
    {
        if (_blobServiceClients.TryGetValue(accountName, out BlobServiceClient? existingClient))
            return existingClient;

        var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
        var client = new BlobServiceClient(serviceUri, await armClientFactory.GetCredentialAsync());
        _blobServiceClients[accountName] = client;

        return client;
    }

    #region Storage Account Operations

    public async Task<IEnumerable<StorageAccountDto>> ListStorageAccountsAsync(string? subscriptionId = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var accounts = new List<StorageAccountDto>();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                await foreach (SubscriptionResource subscription in armClient.GetSubscriptions())
                {
                    accounts.AddRange(subscription.GetStorageAccounts().Select(MapStorageAccount));
                }
            }
            else
            {
                SubscriptionResource subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                accounts.AddRange(subscription.GetStorageAccounts().Select(MapStorageAccount));
            }

            logger.LogInformation("Retrieved {Count} storage accounts", accounts.Count);
            return accounts;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing storage accounts");
            throw;
        }
    }

    public async Task<StorageAccountDto?> GetStorageAccountAsync(string subscriptionId, string resourceGroupName, string accountName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceGroupResource resourceGroup = armClient.GetResourceGroupResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));

            StorageAccountResource account = await resourceGroup.GetStorageAccounts().GetAsync(accountName);
            return MapStorageAccount(account);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Storage account {AccountName} not found", accountName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving storage account {AccountName}", accountName);
            throw;
        }
    }

    #endregion

    #region Container Operations

    public async Task<IEnumerable<BlobContainerDto>> ListContainersAsync(string accountName, string? prefix = null)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            var containers = new List<BlobContainerDto>();

            await foreach (BlobContainerItem container in serviceClient.GetBlobContainersAsync(prefix: prefix))
            {
                containers.Add(MapBlobContainer(container, accountName));
            }

            logger.LogInformation("Retrieved {Count} containers from {AccountName}", containers.Count, accountName);
            return containers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing containers in {AccountName}", accountName);
            throw;
        }
    }

    public async Task<BlobContainerDto?> GetContainerAsync(string accountName, string containerName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);

            if (!await containerClient.ExistsAsync())
                return null;

            BlobContainerProperties properties = await containerClient.GetPropertiesAsync();
            return MapBlobContainerProperties(properties, containerName, accountName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting container {ContainerName} in {AccountName}", containerName, accountName);
            throw;
        }
    }

    public async Task<BlobContainerDto> CreateContainerAsync(string accountName, string containerName, Dictionary<string, string>? metadata = null)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);

            await containerClient.CreateIfNotExistsAsync(metadata: metadata);
            BlobContainerProperties properties = await containerClient.GetPropertiesAsync();

            logger.LogInformation("Created container {ContainerName} in {AccountName}", containerName, accountName);
            return MapBlobContainerProperties(properties, containerName, accountName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating container {ContainerName} in {AccountName}", containerName, accountName);
            throw;
        }
    }

    public async Task<bool> DeleteContainerAsync(string accountName, string containerName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);

            Response<bool> response = await containerClient.DeleteIfExistsAsync();
            bool deleted = response.Value;

            if (deleted)
                logger.LogInformation("Deleted container {ContainerName} from {AccountName}", containerName, accountName);

            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting container {ContainerName} from {AccountName}", containerName, accountName);
            throw;
        }
    }

    public async Task<bool> ContainerExistsAsync(string accountName, string containerName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            return await containerClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if container {ContainerName} exists in {AccountName}", containerName, accountName);
            throw;
        }
    }

    #endregion

    #region Blob Operations

    public async Task<IEnumerable<BlobItemDto>> ListBlobsAsync(string accountName, string containerName, string? prefix = null, int? maxResults = null)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            var blobs = new List<BlobItemDto>();

            await foreach (BlobItem blob in containerClient.GetBlobsAsync(prefix: prefix))
            {
                blobs.Add(MapBlobItem(blob, containerName, accountName));

                if (maxResults.HasValue && blobs.Count >= maxResults.Value)
                    break;
            }

            logger.LogInformation("Retrieved {Count} blobs from {ContainerName}/{AccountName}", blobs.Count, containerName, accountName);
            return blobs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing blobs in {ContainerName}/{AccountName}", containerName, accountName);
            throw;
        }
    }

    public async Task<BlobPropertiesDto?> GetBlobPropertiesAsync(string accountName, string containerName, string blobName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
                return null;

            BlobProperties properties = await blobClient.GetPropertiesAsync();
            return MapBlobProperties(properties, blobName, containerName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting blob properties for {BlobName} in {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<string> DownloadBlobAsTextAsync(string accountName, string containerName, string blobName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            Response<BlobDownloadResult> response = await blobClient.DownloadContentAsync();
            string content = response.Value.Content.ToString();

            logger.LogInformation("Downloaded blob {BlobName} as text from {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading blob {BlobName} as text from {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<byte[]> DownloadBlobAsBytesAsync(string accountName, string containerName, string blobName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            Response<BlobDownloadResult> response = await blobClient.DownloadContentAsync();
            byte[] content = response.Value.Content.ToArray();

            logger.LogInformation("Downloaded blob {BlobName} as bytes from {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading blob {BlobName} as bytes from {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<BlobItemDto> UploadBlobFromTextAsync(string accountName, string containerName, string blobName, string content, string? contentType = null)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType ?? "text/plain"
                }
            };

            await blobClient.UploadAsync(stream, options);
            BlobProperties properties = await blobClient.GetPropertiesAsync();

            logger.LogInformation("Uploaded blob {BlobName} to {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);

            return new BlobItemDto
            {
                Name = blobName,
                ContainerName = containerName,
                StorageAccountName = accountName,
                ContentLength = properties.ContentLength,
                ContentType = properties.ContentType,
                LastModified = properties.LastModified.DateTime,
                ETag = properties.ETag.ToString()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading blob {BlobName} to {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<BlobItemDto> UploadBlobFromBytesAsync(string accountName, string containerName, string blobName, byte[] content, string? contentType = null)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(content);

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType ?? "application/octet-stream"
                }
            };

            await blobClient.UploadAsync(stream, options);
            BlobProperties properties = await blobClient.GetPropertiesAsync();

            logger.LogInformation("Uploaded blob {BlobName} to {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);

            return new BlobItemDto
            {
                Name = blobName,
                ContainerName = containerName,
                StorageAccountName = accountName,
                ContentLength = properties.ContentLength,
                ContentType = properties.ContentType,
                LastModified = properties.LastModified.DateTime,
                ETag = properties.ETag.ToString()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading blob {BlobName} to {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<bool> DeleteBlobAsync(string accountName, string containerName, string blobName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            Response<bool> response = await blobClient.DeleteIfExistsAsync();
            bool deleted = response.Value;

            if (deleted)
                logger.LogInformation("Deleted blob {BlobName} from {ContainerName}/{AccountName}", 
                    blobName, containerName, accountName);

            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting blob {BlobName} from {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<bool> BlobExistsAsync(string accountName, string containerName, string blobName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            return await blobClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if blob {BlobName} exists in {ContainerName}/{AccountName}", 
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<BlobItemDto> CopyBlobAsync(string sourceAccountName, string sourceContainerName, string sourceBlobName,
        string destAccountName, string destContainerName, string destBlobName)
    {
        try
        {
            BlobServiceClient sourceServiceClient = await GetBlobServiceClientAsync(sourceAccountName);
            BlobContainerClient sourceContainerClient = sourceServiceClient.GetBlobContainerClient(sourceContainerName);
            BlobClient sourceBlobClient = sourceContainerClient.GetBlobClient(sourceBlobName);

            BlobServiceClient destServiceClient = await GetBlobServiceClientAsync(destAccountName);
            BlobContainerClient destContainerClient = destServiceClient.GetBlobContainerClient(destContainerName);
            BlobClient destBlobClient = destContainerClient.GetBlobClient(destBlobName);

            await destBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

            // Wait for copy to complete
            BlobProperties properties = await destBlobClient.GetPropertiesAsync();
            while (properties.CopyStatus == CopyStatus.Pending)
            {
                await Task.Delay(500);
                properties = await destBlobClient.GetPropertiesAsync();
            }

            logger.LogInformation("Copied blob from {SourceContainer}/{SourceBlob} to {DestContainer}/{DestBlob}",
                sourceContainerName, sourceBlobName, destContainerName, destBlobName);

            return new BlobItemDto
            {
                Name = destBlobName,
                ContainerName = destContainerName,
                StorageAccountName = destAccountName,
                ContentLength = properties.ContentLength,
                ContentType = properties.ContentType,
                LastModified = properties.LastModified.DateTime,
                ETag = properties.ETag.ToString()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error copying blob from {SourceContainer}/{SourceBlob} to {DestContainer}/{DestBlob}",
                sourceContainerName, sourceBlobName, destContainerName, destBlobName);
            throw;
        }
    }

    #endregion

    #region Blob Metadata Operations

    public async Task<Dictionary<string, string>> GetBlobMetadataAsync(string accountName, string containerName, string blobName)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            BlobProperties properties = await blobClient.GetPropertiesAsync();
            return properties.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting blob metadata for {BlobName} in {ContainerName}/{AccountName}",
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task SetBlobMetadataAsync(string accountName, string containerName, string blobName, Dictionary<string, string> metadata)
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.SetMetadataAsync(metadata);

            logger.LogInformation("Set metadata for blob {BlobName} in {ContainerName}/{AccountName}",
                blobName, containerName, accountName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting blob metadata for {BlobName} in {ContainerName}/{AccountName}",
                blobName, containerName, accountName);
            throw;
        }
    }

    #endregion

    #region SAS Token Operations

    public async Task<SasTokenDto> GenerateBlobSasUrlAsync(string accountName, string containerName, string blobName,
        int expirationHours = 1, string permissions = "r")
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            if (!blobClient.CanGenerateSasUri)
            {
                throw new InvalidOperationException("Cannot generate SAS token. Ensure proper authentication is configured.");
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(expirationHours)
            };

            // Set permissions
            if (permissions.Contains('r')) sasBuilder.SetPermissions(BlobSasPermissions.Read);
            if (permissions.Contains('w')) sasBuilder.SetPermissions(BlobSasPermissions.Write);
            if (permissions.Contains('d')) sasBuilder.SetPermissions(BlobSasPermissions.Delete);
            if (permissions.Contains('a')) sasBuilder.SetPermissions(BlobSasPermissions.Add);
            if (permissions.Contains('c')) sasBuilder.SetPermissions(BlobSasPermissions.Create);

            Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

            logger.LogInformation("Generated SAS URL for blob {BlobName} in {ContainerName}/{AccountName}",
                blobName, containerName, accountName);

            return new SasTokenDto
            {
                Uri = sasUri.ToString(),
                SasToken = sasUri.Query,
                ExpiresOn = sasBuilder.ExpiresOn.DateTime,
                Permissions = permissions,
                BlobName = blobName,
                ContainerName = containerName,
                StorageAccountName = accountName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for blob {BlobName} in {ContainerName}/{AccountName}",
                blobName, containerName, accountName);
            throw;
        }
    }

    public async Task<SasTokenDto> GenerateContainerSasUrlAsync(string accountName, string containerName,
        int expirationHours = 1, string permissions = "rl")
    {
        try
        {
            BlobServiceClient serviceClient = await GetBlobServiceClientAsync(accountName);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);

            if (!containerClient.CanGenerateSasUri)
            {
                throw new InvalidOperationException("Cannot generate SAS token. Ensure proper authentication is configured.");
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                Resource = "c",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(expirationHours)
            };

            // Set permissions
            if (permissions.Contains('r')) sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
            if (permissions.Contains('w')) sasBuilder.SetPermissions(BlobContainerSasPermissions.Write);
            if (permissions.Contains('d')) sasBuilder.SetPermissions(BlobContainerSasPermissions.Delete);
            if (permissions.Contains('l')) sasBuilder.SetPermissions(BlobContainerSasPermissions.List);
            if (permissions.Contains('a')) sasBuilder.SetPermissions(BlobContainerSasPermissions.Add);
            if (permissions.Contains('c')) sasBuilder.SetPermissions(BlobContainerSasPermissions.Create);

            Uri sasUri = containerClient.GenerateSasUri(sasBuilder);

            logger.LogInformation("Generated SAS URL for container {ContainerName} in {AccountName}",
                containerName, accountName);

            return new SasTokenDto
            {
                Uri = sasUri.ToString(),
                SasToken = sasUri.Query,
                ExpiresOn = sasBuilder.ExpiresOn.DateTime,
                Permissions = permissions,
                BlobName = string.Empty,
                ContainerName = containerName,
                StorageAccountName = accountName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for container {ContainerName} in {AccountName}",
                containerName, accountName);
            throw;
        }
    }

    #endregion

    #region Mapping Methods

    private static StorageAccountDto MapStorageAccount(StorageAccountResource account)
    {
        return new StorageAccountDto
        {
            Id = account.Id.ToString(),
            Name = account.Data.Name,
            Location = account.Data.Location.Name,
            ResourceGroup = account.Id.ResourceGroupName ?? string.Empty,
            SubscriptionId = account.Id.SubscriptionId ?? string.Empty,
            Sku = account.Data.Sku.Name.ToString(),
            Kind = account.Data.Kind.ToString() ?? string.Empty,
            ProvisioningState = account.Data.ProvisioningState?.ToString(),
            PrimaryLocation = account.Data.PrimaryLocation,
            SecondaryLocation = account.Data.SecondaryLocation,
            CreationTime = account.Data.CreatedOn?.DateTime,
            Tags = account.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            EnableHttpsTrafficOnly = account.Data.EnableHttpsTrafficOnly,
            MinimumTlsVersion = account.Data.MinimumTlsVersion?.ToString(),
            AllowBlobPublicAccess = account.Data.AllowBlobPublicAccess
        };
    }

    private static BlobContainerDto MapBlobContainer(BlobContainerItem container, string accountName)
    {
        return new BlobContainerDto
        {
            Name = container.Name,
            StorageAccountName = accountName,
            PublicAccess = container.Properties.PublicAccess?.ToString(),
            LastModified = container.Properties.LastModified.DateTime,
            LeaseStatus = container.Properties.LeaseStatus?.ToString(),
            LeaseState = container.Properties.LeaseState?.ToString(),
            Metadata = container.Properties.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            HasImmutabilityPolicy = container.Properties.HasImmutabilityPolicy,
            HasLegalHold = container.Properties.HasLegalHold,
            DefaultEncryptionScope = container.Properties.DefaultEncryptionScope
        };
    }

    private static BlobContainerDto MapBlobContainerProperties(BlobContainerProperties properties, string containerName, string accountName)
    {
        return new BlobContainerDto
        {
            Name = containerName,
            StorageAccountName = accountName,
            PublicAccess = properties.PublicAccess.ToString(),
            LastModified = properties.LastModified.DateTime,
            LeaseStatus = properties.LeaseStatus.ToString(),
            LeaseState = properties.LeaseState.ToString(),
            Metadata = properties.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            HasImmutabilityPolicy = properties.HasImmutabilityPolicy,
            HasLegalHold = properties.HasLegalHold,
            DefaultEncryptionScope = properties.DefaultEncryptionScope
        };
    }

    private static BlobItemDto MapBlobItem(BlobItem blob, string containerName, string accountName)
    {
        return new BlobItemDto
        {
            Name = blob.Name,
            ContainerName = containerName,
            StorageAccountName = accountName,
            BlobType = blob.Properties.BlobType?.ToString(),
            ContentLength = blob.Properties.ContentLength,
            ContentType = blob.Properties.ContentType,
            ContentEncoding = blob.Properties.ContentEncoding,
            ContentLanguage = blob.Properties.ContentLanguage,
            ContentMd5 = blob.Properties.ContentHash is not null ? Convert.ToBase64String(blob.Properties.ContentHash) : null,
            CacheControl = blob.Properties.CacheControl,
            CreatedOn = blob.Properties.CreatedOn?.DateTime,
            LastModified = blob.Properties.LastModified?.DateTime,
            ETag = blob.Properties.ETag?.ToString(),
            LeaseStatus = blob.Properties.LeaseStatus?.ToString(),
            LeaseState = blob.Properties.LeaseState?.ToString(),
            AccessTier = blob.Properties.AccessTier?.ToString(),
            IsServerEncrypted = blob.Properties.ServerEncrypted,
            Metadata = blob.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Tags = blob.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    private static BlobPropertiesDto MapBlobProperties(BlobProperties properties, string blobName, string containerName)
    {
        return new BlobPropertiesDto
        {
            Name = blobName,
            ContainerName = containerName,
            BlobType = properties.BlobType.ToString(),
            ContentLength = properties.ContentLength,
            ContentType = properties.ContentType,
            ContentEncoding = properties.ContentEncoding,
            ContentLanguage = properties.ContentLanguage,
            ContentDisposition = properties.ContentDisposition,
            CacheControl = properties.CacheControl,
            ContentHash = properties.ContentHash,
            ETag = properties.ETag.ToString(),
            CreatedOn = properties.CreatedOn.DateTime,
            LastModified = properties.LastModified.DateTime,
            LastAccessedOn = properties.LastAccessed.DateTime,
            LeaseStatus = properties.LeaseStatus.ToString(),
            LeaseState = properties.LeaseState.ToString(),
            LeaseDuration = properties.LeaseDuration.ToString(),
            AccessTier = properties.AccessTier,
            IsServerEncrypted = properties.IsServerEncrypted,
            EncryptionKeySha256 = properties.EncryptionKeySha256,
            EncryptionScope = properties.EncryptionScope,
            Metadata = properties.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    #endregion
}