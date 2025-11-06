using System.Text;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Sas;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Storage.Models;

// ReSharper disable InconsistentNaming

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.Storage;

public class FileStorageService(
    ArmClientFactory armClientFactory,
    ILogger<FileStorageService> logger) : IFileStorageService
{
    private readonly Dictionary<string, ShareServiceClient> _shareServiceClients = new();

    private async Task<ShareServiceClient> GetShareServiceClientAsync(string accountName)
    {
        if (_shareServiceClients.TryGetValue(accountName, out ShareServiceClient? existingClient))
            return existingClient;

        var serviceUri = new Uri($"https://{accountName}.file.core.windows.net");
        var client = new ShareServiceClient(serviceUri, await armClientFactory.GetCredentialAsync());
        _shareServiceClients[accountName] = client;

        return client;
    }

    #region File Share Operations

    public async Task<IEnumerable<FileShareDto>> ListFileSharesAsync(string accountName, string? prefix = null)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            var shares = new List<FileShareDto>();

            await foreach (ShareItem? share in serviceClient.GetSharesAsync(prefix: prefix))
            {
                shares.Add(MapFileShare(share, accountName));
            }

            logger.LogInformation("Retrieved {Count} file shares from {AccountName}", shares.Count, accountName);
            return shares;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing file shares in {AccountName}", accountName);
            throw;
        }
    }

    public async Task<FileShareDto?> GetFileShareAsync(string accountName, string shareName)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);

            if (!await shareClient.ExistsAsync())
                return null;

            ShareProperties properties = await shareClient.GetPropertiesAsync();
            return MapFileShareProperties(properties, shareName, accountName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file share {ShareName} in {AccountName}", shareName, accountName);
            throw;
        }
    }

    public async Task<FileShareDto> CreateFileShareAsync(string accountName, string shareName, int? quotaInGB = null, Dictionary<string, string>? metadata = null)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);

            var options = new ShareCreateOptions
            {
                Metadata = metadata
            };

            if (quotaInGB.HasValue)
            {
                options.QuotaInGB = quotaInGB.Value;
            }

            await shareClient.CreateIfNotExistsAsync(options);
            ShareProperties properties = await shareClient.GetPropertiesAsync();

            logger.LogInformation("Created file share {ShareName} in {AccountName}", shareName, accountName);
            return MapFileShareProperties(properties, shareName, accountName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating file share {ShareName} in {AccountName}", shareName, accountName);
            throw;
        }
    }

    public async Task<bool> DeleteFileShareAsync(string accountName, string shareName)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);

            Response<bool>? response = await shareClient.DeleteIfExistsAsync();
            bool deleted = response.Value;

            if (deleted)
                logger.LogInformation("Deleted file share {ShareName} from {AccountName}", shareName, accountName);

            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file share {ShareName} from {AccountName}", shareName, accountName);
            throw;
        }
    }

    public async Task<bool> FileShareExistsAsync(string accountName, string shareName)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            return await shareClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if file share {ShareName} exists in {AccountName}", shareName, accountName);
            throw;
        }
    }

    #endregion

    #region Directory Operations

    public async Task<bool> CreateDirectoryAsync(string accountName, string shareName, string directoryPath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareDirectoryClient? directoryClient = shareClient.GetDirectoryClient(directoryPath);

            Response<ShareDirectoryInfo> response = await directoryClient.CreateIfNotExistsAsync();
            bool created = response is not null;

            if (created)
                logger.LogInformation("Created directory {DirectoryPath} in {ShareName}/{AccountName}", directoryPath, shareName, accountName);

            return created;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating directory {DirectoryPath} in {ShareName}/{AccountName}", directoryPath, shareName, accountName);
            throw;
        }
    }

    public async Task<bool> DeleteDirectoryAsync(string accountName, string shareName, string directoryPath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareDirectoryClient? directoryClient = shareClient.GetDirectoryClient(directoryPath);

            Response<bool>? response = await directoryClient.DeleteIfExistsAsync();
            bool deleted = response.Value;

            if (deleted)
                logger.LogInformation("Deleted directory {DirectoryPath} from {ShareName}/{AccountName}", directoryPath, shareName, accountName);

            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting directory {DirectoryPath} from {ShareName}/{AccountName}", directoryPath, shareName, accountName);
            throw;
        }
    }

    public async Task<bool> DirectoryExistsAsync(string accountName, string shareName, string directoryPath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareDirectoryClient? directoryClient = shareClient.GetDirectoryClient(directoryPath);
            return await directoryClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if directory {DirectoryPath} exists in {ShareName}/{AccountName}", directoryPath, shareName, accountName);
            throw;
        }
    }

    #endregion

    #region File Operations

    public async Task<IEnumerable<FileItemDto>> ListFilesAndDirectoriesAsync(string accountName, string shareName, string? directoryPath = null, string? prefix = null)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareDirectoryClient? directoryClient = string.IsNullOrEmpty(directoryPath)
                ? shareClient.GetRootDirectoryClient()
                : shareClient.GetDirectoryClient(directoryPath);

            var items = new List<FileItemDto>();

            await foreach (ShareFileItem? item in directoryClient.GetFilesAndDirectoriesAsync(prefix: prefix))
            {
                items.Add(MapFileItem(item, shareName, accountName, directoryPath));
            }

            logger.LogInformation("Retrieved {Count} items from {ShareName}/{DirectoryPath} in {AccountName}", 
                items.Count, shareName, directoryPath ?? "root", accountName);
            return items;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing files in {ShareName}/{DirectoryPath} in {AccountName}", 
                shareName, directoryPath ?? "root", accountName);
            throw;
        }
    }

    public async Task<FilePropertiesDto?> GetFilePropertiesAsync(string accountName, string shareName, string filePath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            if (!await fileClient.ExistsAsync())
                return null;

            ShareFileProperties properties = await fileClient.GetPropertiesAsync();
            return MapFileProperties(properties, filePath, shareName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file properties for {FilePath} in {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task<string> DownloadFileAsTextAsync(string accountName, string shareName, string filePath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            Response<ShareFileDownloadInfo> response = await fileClient.DownloadAsync();
            using var streamReader = new StreamReader(response.Value.Content);
            string content = await streamReader.ReadToEndAsync();

            logger.LogInformation("Downloaded file {FilePath} as text from {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading file {FilePath} as text from {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task<byte[]> DownloadFileAsBytesAsync(string accountName, string shareName, string filePath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            Response<ShareFileDownloadInfo> response = await fileClient.DownloadAsync();
            using var memoryStream = new MemoryStream();
            await response.Value.Content.CopyToAsync(memoryStream);
            byte[] content = memoryStream.ToArray();

            logger.LogInformation("Downloaded file {FilePath} as bytes from {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading file {FilePath} as bytes from {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task<FileItemDto> UploadFileFromTextAsync(string accountName, string shareName, string filePath, string content, string? contentType = null)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);

            await fileClient.CreateAsync(bytes.Length);
            
            var httpHeaders = new ShareFileHttpHeaders
            {
                ContentType = contentType ?? "text/plain"
            };
            
            await fileClient.SetHttpHeadersAsync(new ShareFileSetHttpHeadersOptions { HttpHeaders = httpHeaders });
            await fileClient.UploadAsync(stream);

            ShareFileProperties properties = await fileClient.GetPropertiesAsync();

            logger.LogInformation("Uploaded file {FilePath} to {ShareName}/{AccountName}", 
                filePath, shareName, accountName);

            return new FileItemDto
            {
                Name = Path.GetFileName(filePath),
                ShareName = shareName,
                StorageAccountName = accountName,
                Path = filePath,
                IsDirectory = false,
                ContentLength = properties.ContentLength,
                ContentType = properties.ContentType,
                LastModified = properties.LastModified.DateTime,
                ETag = properties.ETag.ToString()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file {FilePath} to {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task<FileItemDto> UploadFileFromBytesAsync(string accountName, string shareName, string filePath, byte[] content, string? contentType = null)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            using var stream = new MemoryStream(content);

            await fileClient.CreateAsync(content.Length);
            
            var httpHeaders = new ShareFileHttpHeaders
            {
                ContentType = contentType ?? "application/octet-stream"
            };
            
            await fileClient.SetHttpHeadersAsync(new ShareFileSetHttpHeadersOptions { HttpHeaders = httpHeaders });
            await fileClient.UploadAsync(stream);

            ShareFileProperties properties = await fileClient.GetPropertiesAsync();

            logger.LogInformation("Uploaded file {FilePath} to {ShareName}/{AccountName}", 
                filePath, shareName, accountName);

            return new FileItemDto
            {
                Name = Path.GetFileName(filePath),
                ShareName = shareName,
                StorageAccountName = accountName,
                Path = filePath,
                IsDirectory = false,
                ContentLength = properties.ContentLength,
                ContentType = properties.ContentType,
                LastModified = properties.LastModified.DateTime,
                ETag = properties.ETag.ToString()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file {FilePath} to {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string accountName, string shareName, string filePath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            Response<bool>? response = await fileClient.DeleteIfExistsAsync();
            bool deleted = response.Value;

            if (deleted)
                logger.LogInformation("Deleted file {FilePath} from {ShareName}/{AccountName}", 
                    filePath, shareName, accountName);

            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file {FilePath} from {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string accountName, string shareName, string filePath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            return await fileClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if file {FilePath} exists in {ShareName}/{AccountName}", 
                filePath, shareName, accountName);
            throw;
        }
    }

    #endregion

    #region Metadata Operations

    public async Task<Dictionary<string, string>> GetFileMetadataAsync(string accountName, string shareName, string filePath)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            ShareFileProperties properties = await fileClient.GetPropertiesAsync();
            return properties.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file metadata for {FilePath} in {ShareName}/{AccountName}",
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task SetFileMetadataAsync(string accountName, string shareName, string filePath, Dictionary<string, string> metadata)
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            await fileClient.SetMetadataAsync(metadata);

            logger.LogInformation("Set metadata for file {FilePath} in {ShareName}/{AccountName}",
                filePath, shareName, accountName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting file metadata for {FilePath} in {ShareName}/{AccountName}",
                filePath, shareName, accountName);
            throw;
        }
    }

    #endregion

    #region SAS Token Operations

    public async Task<SasTokenDto> GenerateFileSasUrlAsync(string accountName, string shareName, string filePath,
        int expirationHours = 1, string permissions = "r")
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);
            ShareFileClient? fileClient = shareClient.GetRootDirectoryClient().GetFileClient(filePath);

            if (!fileClient.CanGenerateSasUri)
            {
                throw new InvalidOperationException("Cannot generate SAS token. Ensure proper authentication is configured.");
            }

            var sasBuilder = new ShareSasBuilder
            {
                ShareName = shareName,
                FilePath = filePath,
                Resource = "f",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(expirationHours)
            };

            // Set permissions
            if (permissions.Contains('r')) sasBuilder.SetPermissions(ShareFileSasPermissions.Read);
            if (permissions.Contains('w')) sasBuilder.SetPermissions(ShareFileSasPermissions.Write);
            if (permissions.Contains('d')) sasBuilder.SetPermissions(ShareFileSasPermissions.Delete);
            if (permissions.Contains('c')) sasBuilder.SetPermissions(ShareFileSasPermissions.Create);

            Uri? sasUri = fileClient.GenerateSasUri(sasBuilder);

            logger.LogInformation("Generated SAS URL for file {FilePath} in {ShareName}/{AccountName}",
                filePath, shareName, accountName);

            return new SasTokenDto
            {
                Uri = sasUri.ToString(),
                SasToken = sasUri.Query,
                ExpiresOn = sasBuilder.ExpiresOn.DateTime,
                Permissions = permissions,
                BlobName = filePath,
                ContainerName = shareName,
                StorageAccountName = accountName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for file {FilePath} in {ShareName}/{AccountName}",
                filePath, shareName, accountName);
            throw;
        }
    }

    public async Task<SasTokenDto> GenerateShareSasUrlAsync(string accountName, string shareName,
        int expirationHours = 1, string permissions = "rl")
    {
        try
        {
            ShareServiceClient serviceClient = await GetShareServiceClientAsync(accountName);
            ShareClient? shareClient = serviceClient.GetShareClient(shareName);

            if (!shareClient.CanGenerateSasUri)
            {
                throw new InvalidOperationException("Cannot generate SAS token. Ensure proper authentication is configured.");
            }

            var sasBuilder = new ShareSasBuilder
            {
                ShareName = shareName,
                Resource = "s",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(expirationHours)
            };

            // Set permissions
            if (permissions.Contains('r')) sasBuilder.SetPermissions(ShareSasPermissions.Read);
            if (permissions.Contains('w')) sasBuilder.SetPermissions(ShareSasPermissions.Write);
            if (permissions.Contains('d')) sasBuilder.SetPermissions(ShareSasPermissions.Delete);
            if (permissions.Contains('l')) sasBuilder.SetPermissions(ShareSasPermissions.List);
            if (permissions.Contains('c')) sasBuilder.SetPermissions(ShareSasPermissions.Create);

            Uri? sasUri = shareClient.GenerateSasUri(sasBuilder);

            logger.LogInformation("Generated SAS URL for share {ShareName} in {AccountName}",
                shareName, accountName);

            return new SasTokenDto
            {
                Uri = sasUri.ToString(),
                SasToken = sasUri.Query,
                ExpiresOn = sasBuilder.ExpiresOn.DateTime,
                Permissions = permissions,
                BlobName = string.Empty,
                ContainerName = shareName,
                StorageAccountName = accountName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for share {ShareName} in {AccountName}",
                shareName, accountName);
            throw;
        }
    }

    #endregion

    #region Mapping Methods

    private static FileShareDto MapFileShare(ShareItem share, string accountName)
    {
        return new FileShareDto
        {
            Name = share.Name,
            StorageAccountName = accountName,
            QuotaInGB = share.Properties.QuotaInGB,
            LastModified = share.Properties.LastModified?.DateTime,
            ETag = share.Properties.ETag?.ToString(),
            Metadata = share.Properties.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            AccessTier = share.Properties.AccessTier
        };
    }

    private static FileShareDto MapFileShareProperties(ShareProperties properties, string shareName, string accountName)
    {
        return new FileShareDto
        {
            Name = shareName,
            StorageAccountName = accountName,
            QuotaInGB = properties.QuotaInGB,
            LastModified = properties.LastModified?.DateTime,
            ETag = properties.ETag.ToString(),
            Metadata = properties.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            AccessTier = properties.AccessTier
        };
    }

    private static FileItemDto MapFileItem(ShareFileItem item, string shareName, string accountName, string? directoryPath)
    {
        string? fullPath = string.IsNullOrEmpty(directoryPath) 
            ? item.Name 
            : $"{directoryPath}/{item.Name}";

        return new FileItemDto
        {
            Name = item.Name,
            ShareName = shareName,
            StorageAccountName = accountName,
            Path = fullPath,
            IsDirectory = item.IsDirectory,
            ContentLength = item.IsDirectory ? null : item.FileSize,
            LastModified = null,
            ETag = null
        };
    }

    private static FilePropertiesDto MapFileProperties(ShareFileProperties properties, string filePath, string shareName)
    {
        return new FilePropertiesDto
        {
            Name = Path.GetFileName(filePath),
            ShareName = shareName,
            Path = filePath,
            ContentLength = properties.ContentLength,
            ContentType = properties.ContentType,
            ContentEncoding = properties.ContentEncoding is not null ? string.Join(", ", properties.ContentEncoding) : null,
            ContentLanguage = properties.ContentLanguage is not null ? string.Join(", ", properties.ContentLanguage) : null,
            ContentDisposition = properties.ContentDisposition,
            CacheControl = properties.CacheControl,
            ContentHash = properties.ContentHash,
            ETag = properties.ETag.ToString(),
            CreatedOn = properties.SmbProperties.FileCreatedOn?.DateTime ?? DateTime.MinValue,
            LastModified = properties.LastModified.DateTime,
            IsServerEncrypted = properties.IsServerEncrypted,
            Metadata = properties.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    #endregion
}
