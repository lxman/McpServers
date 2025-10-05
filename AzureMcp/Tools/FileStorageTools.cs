using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.Storage;
using AzureMcp.Services.Storage.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class FileStorageTools(IFileStorageService fileStorageService)
{
    #region File Share Tools

    [McpServerTool]
    [Description("List file shares in a storage account")]
    public async Task<string> ListFileSharesAsync(
        [Description("Storage account name")] string accountName,
        [Description("Optional prefix to filter shares")] string? prefix = null)
    {
        try
        {
            IEnumerable<FileShareDto> shares = await fileStorageService.ListFileSharesAsync(accountName, prefix);
            return JsonSerializer.Serialize(new { success = true, shares = shares.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListFileShares");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific file share")]
    public async Task<string> GetFileShareAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName)
    {
        try
        {
            FileShareDto? share = await fileStorageService.GetFileShareAsync(accountName, shareName);
            if (share == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"File share {shareName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, share },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetFileShare");
        }
    }

    [McpServerTool]
    [Description("Create a new file share")]
    public async Task<string> CreateFileShareAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("Optional quota in GB")] int? quotaInGB = null,
        [Description("Optional metadata as JSON object")] string? metadataJson = null)
    {
        try
        {
            Dictionary<string, string>? metadata = null;
            if (!string.IsNullOrEmpty(metadataJson))
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
            }

            FileShareDto share = await fileStorageService.CreateFileShareAsync(accountName, shareName, quotaInGB, metadata);
            return JsonSerializer.Serialize(new { success = true, share },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateFileShare");
        }
    }

    [McpServerTool]
    [Description("Delete a file share")]
    public async Task<string> DeleteFileShareAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName)
    {
        try
        {
            bool deleted = await fileStorageService.DeleteFileShareAsync(accountName, shareName);
            return JsonSerializer.Serialize(new { success = true, deleted },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteFileShare");
        }
    }

    [McpServerTool]
    [Description("Check if a file share exists")]
    public async Task<string> FileShareExistsAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName)
    {
        try
        {
            bool exists = await fileStorageService.FileShareExistsAsync(accountName, shareName);
            return JsonSerializer.Serialize(new { success = true, exists },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "FileShareExists");
        }
    }

    #endregion

    #region Directory Tools

    [McpServerTool]
    [Description("Create a directory in a file share")]
    public async Task<string> CreateDirectoryAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("Directory path")] string directoryPath)
    {
        try
        {
            bool created = await fileStorageService.CreateDirectoryAsync(accountName, shareName, directoryPath);
            return JsonSerializer.Serialize(new { success = true, created },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateDirectory");
        }
    }

    [McpServerTool]
    [Description("Delete a directory from a file share")]
    public async Task<string> DeleteDirectoryAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("Directory path")] string directoryPath)
    {
        try
        {
            bool deleted = await fileStorageService.DeleteDirectoryAsync(accountName, shareName, directoryPath);
            return JsonSerializer.Serialize(new { success = true, deleted },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteDirectory");
        }
    }

    [McpServerTool]
    [Description("Check if a directory exists")]
    public async Task<string> DirectoryExistsAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("Directory path")] string directoryPath)
    {
        try
        {
            bool exists = await fileStorageService.DirectoryExistsAsync(accountName, shareName, directoryPath);
            return JsonSerializer.Serialize(new { success = true, exists },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DirectoryExists");
        }
    }

    #endregion

    #region File Tools

    [McpServerTool]
    [Description("List files and directories in a file share")]
    public async Task<string> ListFilesAndDirectoriesAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("Optional directory path")] string? directoryPath = null,
        [Description("Optional prefix to filter items")] string? prefix = null)
    {
        try
        {
            IEnumerable<FileItemDto> items = await fileStorageService.ListFilesAndDirectoriesAsync(accountName, shareName, directoryPath, prefix);
            return JsonSerializer.Serialize(new { success = true, items = items.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListFilesAndDirectories");
        }
    }

    [McpServerTool]
    [Description("Get properties of a specific file")]
    public async Task<string> GetFilePropertiesAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath)
    {
        try
        {
            FilePropertiesDto? properties = await fileStorageService.GetFilePropertiesAsync(accountName, shareName, filePath);
            if (properties == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"File {filePath} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, properties },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetFileProperties");
        }
    }

    [McpServerTool]
    [Description("Download a file as text content")]
    public async Task<string> DownloadFileAsTextAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath)
    {
        try
        {
            string content = await fileStorageService.DownloadFileAsTextAsync(accountName, shareName, filePath);
            return JsonSerializer.Serialize(new { success = true, content },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DownloadFileAsText");
        }
    }

    [McpServerTool]
    [Description("Upload text content as a file")]
    public async Task<string> UploadFileFromTextAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath,
        [Description("Text content to upload")] string content,
        [Description("Optional content type (default: text/plain)")] string? contentType = null)
    {
        try
        {
            FileItemDto file = await fileStorageService.UploadFileFromTextAsync(accountName, shareName, filePath, content, contentType);
            return JsonSerializer.Serialize(new { success = true, file },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UploadFileFromText");
        }
    }

    [McpServerTool]
    [Description("Delete a file from a file share")]
    public async Task<string> DeleteFileAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath)
    {
        try
        {
            bool deleted = await fileStorageService.DeleteFileAsync(accountName, shareName, filePath);
            return JsonSerializer.Serialize(new { success = true, deleted },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteFile");
        }
    }

    [McpServerTool]
    [Description("Check if a file exists")]
    public async Task<string> FileExistsAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath)
    {
        try
        {
            bool exists = await fileStorageService.FileExistsAsync(accountName, shareName, filePath);
            return JsonSerializer.Serialize(new { success = true, exists },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "FileExists");
        }
    }

    #endregion

    #region Metadata Tools

    [McpServerTool]
    [Description("Get metadata for a file")]
    public async Task<string> GetFileMetadataAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath)
    {
        try
        {
            Dictionary<string, string> metadata = await fileStorageService.GetFileMetadataAsync(accountName, shareName, filePath);
            return JsonSerializer.Serialize(new { success = true, metadata },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetFileMetadata");
        }
    }

    [McpServerTool]
    [Description("Set metadata for a file")]
    public async Task<string> SetFileMetadataAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath,
        [Description("Metadata as JSON object (e.g., '{\"key\":\"value\"}')")]  string metadataJson)
    {
        try
        {
            Dictionary<string, string>? metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
            if (metadata == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid metadata JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            await fileStorageService.SetFileMetadataAsync(accountName, shareName, filePath, metadata);
            return JsonSerializer.Serialize(new { success = true, message = "Metadata updated successfully" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SetFileMetadata");
        }
    }

    #endregion

    #region SAS Token Tools

    [McpServerTool]
    [Description("Generate a temporary SAS (Shared Access Signature) URL for a file with read access")]
    public async Task<string> GenerateFileSasUrlAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("File path")] string filePath,
        [Description("Expiration time in hours (default: 1)")] int expirationHours = 1,
        [Description("Permissions: r=read, w=write, d=delete, c=create (default: r)")] string permissions = "r")
    {
        try
        {
            SasTokenDto sasToken = await fileStorageService.GenerateFileSasUrlAsync(accountName, shareName, filePath, expirationHours, permissions);
            return JsonSerializer.Serialize(new { success = true, sasToken },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GenerateFileSasUrl");
        }
    }

    [McpServerTool]
    [Description("Generate a temporary SAS (Shared Access Signature) URL for a file share with list and read access")]
    public async Task<string> GenerateShareSasUrlAsync(
        [Description("Storage account name")] string accountName,
        [Description("Share name")] string shareName,
        [Description("Expiration time in hours (default: 1)")] int expirationHours = 1,
        [Description("Permissions: r=read, w=write, d=delete, l=list, c=create (default: rl)")] string permissions = "rl")
    {
        try
        {
            SasTokenDto sasToken = await fileStorageService.GenerateShareSasUrlAsync(accountName, shareName, expirationHours, permissions);
            return JsonSerializer.Serialize(new { success = true, sasToken },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GenerateShareSasUrl");
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
