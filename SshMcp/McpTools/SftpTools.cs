using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SshClient.Core.Models;
using SshClient.Core.Services;

namespace SshMcp.McpTools;

[McpServerToolType]
public sealed class SftpTools(
    SftpFileManager fileManager,
    OutputGuard outputGuard,
    ILogger<SftpTools> logger)
{
    [McpServerTool]
    [DisplayName("sftp_list")]
    [Description("List files and directories at a remote path.")]
    public async Task<string> List(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Remote path to list")] string remotePath)
    {
        try
        {
            SftpListResult result = await fileManager.ListAsync(connectionName, remotePath);
            return result.ToGuardedResponse(outputGuard, "sftp_list");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP list failed");
            return ex.ToErrorResponse(outputGuard, "Check path exists and connection is active");
        }
    }

    [McpServerTool]
    [DisplayName("sftp_download")]
    [Description("Download a file from remote to local path.")]
    public async Task<string> Download(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Remote file path")] string remotePath,
        [Description("Local destination path")] string localPath)
    {
        try
        {
            SftpTransferResult result = await fileManager.DownloadAsync(connectionName, remotePath, localPath);
            return result.ToGuardedResponse(outputGuard, "sftp_download");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP download failed");
            return ex.ToErrorResponse(outputGuard, "Check remote path exists and local path is writable");
        }
    }

    [McpServerTool]
    [DisplayName("sftp_upload")]
    [Description("Upload a file from local to remote path.")]
    public async Task<string> Upload(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Local file path")] string localPath,
        [Description("Remote destination path")] string remotePath)
    {
        try
        {
            SftpTransferResult result = await fileManager.UploadAsync(connectionName, localPath, remotePath);
            return result.ToGuardedResponse(outputGuard, "sftp_upload");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP upload failed");
            return ex.ToErrorResponse(outputGuard, "Check local file exists and remote path is writable");
        }
    }

    [McpServerTool]
    [DisplayName("sftp_read_text")]
    [Description("Read a remote file as text.")]
    public async Task<string> ReadText(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Remote file path")] string remotePath,
        [Description("Maximum bytes to read (default: 1000000)")] int maxBytes = 1_000_000)
    {
        try
        {
            (bool success, string content, string? error) = await fileManager.ReadTextAsync(connectionName, remotePath, maxBytes);

            if (!success)
                return (error ?? "Read failed").ToErrorResponse(outputGuard);

            return new { Content = content, Path = remotePath }.ToGuardedResponse(outputGuard, "sftp_read_text");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP read failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("sftp_write_text")]
    [Description("Write text content to a remote file.")]
    public async Task<string> WriteText(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Remote file path")] string remotePath,
        [Description("Text content to write")] string content)
    {
        try
        {
            (bool success, string? error) = await fileManager.WriteTextAsync(connectionName, remotePath, content);

            return success
                ? new { Message = $"File written: {remotePath}" }.ToSuccessResponse(outputGuard)
                : (error ?? "Write failed").ToErrorResponse(outputGuard);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP write failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("sftp_delete")]
    [Description("Delete a remote file.")]
    public async Task<string> Delete(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Remote file path")] string remotePath)
    {
        try
        {
            (bool success, string? error) = await fileManager.DeleteFileAsync(connectionName, remotePath);

            return success
                ? new { Message = $"File deleted: {remotePath}" }.ToSuccessResponse(outputGuard)
                : (error ?? "Delete failed").ToErrorResponse(outputGuard);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP delete failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("sftp_mkdir")]
    [Description("Create a directory on the remote server.")]
    public async Task<string> CreateDirectory(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Remote directory path")] string remotePath)
    {
        try
        {
            (bool success, string? error) = await fileManager.CreateDirectoryAsync(connectionName, remotePath);

            return success
                ? new { Message = $"Directory created: {remotePath}" }.ToSuccessResponse(outputGuard)
                : (error ?? "Create directory failed").ToErrorResponse(outputGuard);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP mkdir failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }

    [McpServerTool]
    [DisplayName("sftp_exists")]
    [Description("Check if a path exists on the remote server.")]
    public async Task<string> Exists(
        [Description("Name of the SSH connection")] string connectionName,
        [Description("Remote path to check")] string remotePath)
    {
        try
        {
            (bool exists, bool isDirectory, string? error) = await fileManager.ExistsAsync(connectionName, remotePath);

            if (error is not null)
                return error.ToErrorResponse(outputGuard);

            return new { Exists = exists, IsDirectory = isDirectory, Path = remotePath }
                .ToGuardedResponse(outputGuard, "sftp_exists");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP exists check failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }
}
