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
                return ToSftpErrorResponse(error ?? "Read failed", connectionName);

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
                : ToSftpErrorResponse(error ?? "Write failed", connectionName);
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
                : ToSftpErrorResponse(error ?? "Delete failed", connectionName);
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
                : ToSftpErrorResponse(error ?? "Create directory failed", connectionName);
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
                return ToSftpErrorResponse(error, connectionName);

            return new { Exists = exists, IsDirectory = isDirectory, Path = remotePath }
                .ToGuardedResponse(outputGuard, "sftp_exists");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SFTP exists check failed");
            return ex.ToErrorResponse(outputGuard);
        }
    }

    private string ToSftpErrorResponse(string error, string connectionName)
    {
        if (!IsConnectionRecoveryError(error))
            return error.ToErrorResponse(outputGuard);

        bool hasProfile = !error.Contains("no saved profile matched", StringComparison.OrdinalIgnoreCase);
        string errorCode = hasProfile
            ? SshRecoveryCodes.ConnectionNotConnected
            : SshRecoveryCodes.NoMatchingProfile;

        return error.ToErrorResponse(
            outputGuard,
            details: new
            {
                Recoverable = true,
                Recovery = CreateRecoveryGuidance(connectionName, hasProfile)
            },
            suggestion: hasProfile
                ? "Call ssh_connect_profile with this connection/profile name, then retry the original SFTP operation."
                : "Call ssh_list_profiles. If no profile matches and connection details are known, call ssh_connect; otherwise ask the user for host, username, and authentication.",
            errorCode: errorCode);
    }

    private static bool IsConnectionRecoveryError(string error)
    {
        return error.Contains("Connection '", StringComparison.OrdinalIgnoreCase)
               && (error.Contains("not active", StringComparison.OrdinalIgnoreCase)
                   || error.Contains("not connected", StringComparison.OrdinalIgnoreCase));
    }

    private static SshRecoveryGuidance CreateRecoveryGuidance(string connectionName, bool hasProfile)
    {
        if (hasProfile)
        {
            return new SshRecoveryGuidance
            {
                Message = $"Reconnect with the saved profile named '{connectionName}', then retry the original operation.",
                Steps =
                [
                    "Call ssh_connect_profile with the requested connection/profile name.",
                    "If the connection succeeds, retry the original SSH or SFTP operation.",
                    "If reconnect fails, inspect the returned connection error before choosing another transport."
                ],
                Tools = ["ssh_connect_profile"]
            };
        }

        return new SshRecoveryGuidance
        {
            Message = $"No active connection or saved profile matched '{connectionName}'. Establish an MCP SSH connection before retrying.",
            Steps =
            [
                "Call ssh_list_profiles to check for a differently named saved profile that matches the intended host or user.",
                "If a matching profile is found, call ssh_connect_profile with that profile name and retry the original operation.",
                "If no matching profile exists and host, username, and authentication are known from context, call ssh_connect.",
                "If required connection details are missing, ask the user for host, username, and authentication method instead of switching to another SSH mechanism.",
                "After a successful ssh_connect, call ssh_save_profile when this target should be reused."
            ],
            Tools = ["ssh_list_profiles", "ssh_connect_profile", "ssh_connect", "ssh_save_profile"],
            AskUserWhenMissing = ["host", "username", "privateKeyPath or password"]
        };
    }
}
