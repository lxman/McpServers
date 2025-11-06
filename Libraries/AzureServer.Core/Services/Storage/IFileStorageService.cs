using AzureServer.Core.Services.Storage.Models;

namespace AzureServer.Core.Services.Storage;

public interface IFileStorageService
{
    // File Share Operations
    Task<IEnumerable<FileShareDto>> ListFileSharesAsync(string accountName, string? prefix = null);
    Task<FileShareDto?> GetFileShareAsync(string accountName, string shareName);
    Task<FileShareDto> CreateFileShareAsync(string accountName, string shareName, int? quotaInGB = null, Dictionary<string, string>? metadata = null);
    Task<bool> DeleteFileShareAsync(string accountName, string shareName);
    Task<bool> FileShareExistsAsync(string accountName, string shareName);

    // Directory Operations
    Task<bool> CreateDirectoryAsync(string accountName, string shareName, string directoryPath);
    Task<bool> DeleteDirectoryAsync(string accountName, string shareName, string directoryPath);
    Task<bool> DirectoryExistsAsync(string accountName, string shareName, string directoryPath);

    // File Operations
    Task<IEnumerable<FileItemDto>> ListFilesAndDirectoriesAsync(string accountName, string shareName, string? directoryPath = null, string? prefix = null);
    Task<FilePropertiesDto?> GetFilePropertiesAsync(string accountName, string shareName, string filePath);
    Task<string> DownloadFileAsTextAsync(string accountName, string shareName, string filePath);
    Task<byte[]> DownloadFileAsBytesAsync(string accountName, string shareName, string filePath);
    Task<FileItemDto> UploadFileFromTextAsync(string accountName, string shareName, string filePath, string content, string? contentType = null);
    Task<FileItemDto> UploadFileFromBytesAsync(string accountName, string shareName, string filePath, byte[] content, string? contentType = null);
    Task<bool> DeleteFileAsync(string accountName, string shareName, string filePath);
    Task<bool> FileExistsAsync(string accountName, string shareName, string filePath);

    // Metadata Operations
    Task<Dictionary<string, string>> GetFileMetadataAsync(string accountName, string shareName, string filePath);
    Task SetFileMetadataAsync(string accountName, string shareName, string filePath, Dictionary<string, string> metadata);

    // SAS Token Operations
    Task<SasTokenDto> GenerateFileSasUrlAsync(string accountName, string shareName, string filePath, int expirationHours = 1, string permissions = "r");
    Task<SasTokenDto> GenerateShareSasUrlAsync(string accountName, string shareName, int expirationHours = 1, string permissions = "rl");
}
