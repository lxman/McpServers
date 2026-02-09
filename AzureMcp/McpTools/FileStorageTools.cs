using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using AzureServer.Core.Services.Storage;
using AzureServer.Core.Services.Storage.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure File Storage operations
/// </summary>
[McpServerToolType]
public class FileStorageTools(
    IFileStorageService fileStorageService,
    ILogger<FileStorageTools> logger)
{
    #region File Share Operations

    [McpServerTool, DisplayName("list_file_shares")]
    [Description("List file shares. See skills/azure/filestorage/list-shares.md only when using this tool")]
    public async Task<string> ListFileShares(string accountName, string? prefix = null)
    {
        try
        {
            logger.LogDebug("Listing file shares in account {AccountName}", accountName);
            IEnumerable<FileShareDto> shares = await fileStorageService.ListFileSharesAsync(accountName, prefix);

            return JsonSerializer.Serialize(new
            {
                success = true,
                shares = shares.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing file shares in account {AccountName}", accountName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_file_share")]
    [Description("Get file share. See skills/azure/filestorage/get-share.md only when using this tool")]
    public async Task<string> GetFileShare(string accountName, string shareName)
    {
        try
        {
            logger.LogDebug("Getting file share {ShareName}", shareName);
            FileShareDto? share = await fileStorageService.GetFileShareAsync(accountName, shareName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                share
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file share {ShareName}", shareName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("create_file_share")]
    [Description("Create file share. See skills/azure/filestorage/create-share.md only when using this tool")]
    public async Task<string> CreateFileShare(
        string accountName,
        string shareName,
        int? quotaInGB = null,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            logger.LogDebug("Creating file share {ShareName}", shareName);
            FileShareDto share = await fileStorageService.CreateFileShareAsync(accountName, shareName, quotaInGB, metadata);

            return JsonSerializer.Serialize(new
            {
                success = true,
                share
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating file share {ShareName}", shareName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("delete_file_share")]
    [Description("Delete file share. See skills/azure/filestorage/delete-share.md only when using this tool")]
    public async Task<string> DeleteFileShare(string accountName, string shareName)
    {
        try
        {
            logger.LogDebug("Deleting file share {ShareName}", shareName);
            bool deleted = await fileStorageService.DeleteFileShareAsync(accountName, shareName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                deleted
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file share {ShareName}", shareName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("file_share_exists")]
    [Description("Check if file share exists. See skills/azure/filestorage/share-exists.md only when using this tool")]
    public async Task<string> FileShareExists(string accountName, string shareName)
    {
        try
        {
            logger.LogDebug("Checking if file share {ShareName} exists", shareName);
            bool exists = await fileStorageService.FileShareExistsAsync(accountName, shareName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                exists
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if file share {ShareName} exists", shareName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Directory Operations

    [McpServerTool, DisplayName("create_directory")]
    [Description("Create directory in file share. See skills/azure/filestorage/create-directory.md only when using this tool")]
    public async Task<string> CreateDirectory(
        string accountName,
        string shareName,
        string directoryPath)
    {
        try
        {
            logger.LogDebug("Creating directory {DirectoryPath}", directoryPath);
            bool created = await fileStorageService.CreateDirectoryAsync(accountName, shareName, directoryPath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                created
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating directory {DirectoryPath}", directoryPath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("delete_directory")]
    [Description("Delete directory in file share. See skills/azure/filestorage/delete-directory.md only when using this tool")]
    public async Task<string> DeleteDirectory(
        string accountName,
        string shareName,
        string directoryPath)
    {
        try
        {
            logger.LogDebug("Deleting directory {DirectoryPath}", directoryPath);
            bool deleted = await fileStorageService.DeleteDirectoryAsync(accountName, shareName, directoryPath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                deleted
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting directory {DirectoryPath}", directoryPath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion
}