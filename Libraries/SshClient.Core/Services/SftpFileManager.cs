using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using SshClient.Core.Models;

namespace SshClient.Core.Services;

/// <summary>
/// Manages SFTP file operations
/// </summary>
public sealed class SftpFileManager(
    SshConnectionManager connectionManager,
    ILogger<SftpFileManager> logger)
{
    /// <summary>
    /// Lists files and directories at a path
    /// </summary>
    public async Task<SftpListResult> ListAsync(
        string connectionName,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
        {
            return CreateListRecoveryResult(connectionName, remotePath);
        }

        try
        {
            logger.LogInformation("Listing {Path} on {Connection}", remotePath, connectionName);

            List<ISftpFile> items = await Task.Run(() => sftpClient.ListDirectory(remotePath).ToList(), cancellationToken);

            List<SftpFileInfo> files = items
                .Where(f => f.Name != "." && f.Name != "..")
                .Select(f => new SftpFileInfo
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    Size = f.IsRegularFile ? f.Length : 0,
                    IsDirectory = f.IsDirectory,
                    LastModified = f.LastWriteTimeUtc,
                    Permissions = GetPermissionsString(f),
                    OwnerId = f.UserId,
                    GroupId = f.GroupId
                })
                .OrderBy(f => !f.IsDirectory)
                .ThenBy(f => f.Name)
                .ToList();

            return new SftpListResult
            {
                Success = true,
                Path = remotePath,
                Items = files,
                TotalCount = files.Count,
                DirectoryCount = files.Count(f => f.IsDirectory),
                FileCount = files.Count(f => !f.IsDirectory),
                ConnectionName = connectionName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list {Path}", remotePath);
            return new SftpListResult
            {
                Success = false,
                Path = remotePath,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Downloads a file to local path
    /// </summary>
    public async Task<SftpTransferResult> DownloadAsync(
        string connectionName,
        string remotePath,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
        {
            return CreateTransferRecoveryResult(connectionName, remotePath, localPath);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Downloading {Remote} to {Local}", remotePath, localPath);

            // Ensure directory exists
            string? directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using FileStream fileStream = File.Create(localPath);
            await Task.Run(() => sftpClient.DownloadFile(remotePath, fileStream), cancellationToken);

            stopwatch.Stop();
            var fileInfo = new FileInfo(localPath);

            return new SftpTransferResult
            {
                Success = true,
                SourcePath = remotePath,
                DestinationPath = localPath,
                BytesTransferred = fileInfo.Length,
                Duration = stopwatch.Elapsed,
                ConnectionName = connectionName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download {Path}", remotePath);
            return new SftpTransferResult
            {
                Success = false,
                SourcePath = remotePath,
                DestinationPath = localPath,
                Error = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Uploads a file to remote path
    /// </summary>
    public async Task<SftpTransferResult> UploadAsync(
        string connectionName,
        string localPath,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
        {
            return CreateTransferRecoveryResult(connectionName, localPath, remotePath);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(localPath))
            {
                return new SftpTransferResult
                {
                    Success = false,
                    SourcePath = localPath,
                    DestinationPath = remotePath,
                    Error = $"Local file not found: {localPath}"
                };
            }

            logger.LogInformation("Uploading {Local} to {Remote}", localPath, remotePath);

            var fileInfo = new FileInfo(localPath);
            await using FileStream fileStream = File.OpenRead(localPath);
            await Task.Run(() => sftpClient.UploadFile(fileStream, remotePath, true), cancellationToken);

            stopwatch.Stop();

            return new SftpTransferResult
            {
                Success = true,
                SourcePath = localPath,
                DestinationPath = remotePath,
                BytesTransferred = fileInfo.Length,
                Duration = stopwatch.Elapsed,
                ConnectionName = connectionName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload {Path}", localPath);
            return new SftpTransferResult
            {
                Success = false,
                SourcePath = localPath,
                DestinationPath = remotePath,
                Error = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Reads file content as text
    /// </summary>
    public async Task<(bool Success, string Content, string? Error)> ReadTextAsync(
        string connectionName,
        string remotePath,
        int maxBytes = 1_000_000,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
            return (false, string.Empty, CreateConnectionRecoveryMessage(connectionName));

        try
        {
            logger.LogInformation("Reading {Path} on {Connection}", remotePath, connectionName);

            string content = await Task.Run(() =>
            {
                using SftpFileStream stream = sftpClient.OpenRead(remotePath);
                using var reader = new StreamReader(stream);

                var buffer = new char[maxBytes];
                int read = reader.Read(buffer, 0, maxBytes);
                return new string(buffer, 0, read);
            }, cancellationToken);

            return (true, content, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read {Path}", remotePath);
            return (false, string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// Writes text content to a file
    /// </summary>
    public async Task<(bool Success, string? Error)> WriteTextAsync(
        string connectionName,
        string remotePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
            return (false, CreateConnectionRecoveryMessage(connectionName));

        try
        {
            logger.LogInformation("Writing {Path} on {Connection}", remotePath, connectionName);

            await Task.Run(() => sftpClient.WriteAllText(remotePath, content), cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write {Path}", remotePath);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Deletes a file
    /// </summary>
    public async Task<(bool Success, string? Error)> DeleteFileAsync(
        string connectionName,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
            return (false, CreateConnectionRecoveryMessage(connectionName));

        try
        {
            logger.LogInformation("Deleting {Path} on {Connection}", remotePath, connectionName);
            await Task.Run(() => sftpClient.DeleteFile(remotePath), cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete {Path}", remotePath);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Creates a directory
    /// </summary>
    public async Task<(bool Success, string? Error)> CreateDirectoryAsync(
        string connectionName,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
            return (false, CreateConnectionRecoveryMessage(connectionName));

        try
        {
            logger.LogInformation("Creating directory {Path} on {Connection}", remotePath, connectionName);
            await Task.Run(() => sftpClient.CreateDirectory(remotePath), cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create directory {Path}", remotePath);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Checks if a path exists
    /// </summary>
    public async Task<(bool Exists, bool IsDirectory, string? Error)> ExistsAsync(
        string connectionName,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        SftpClient? sftpClient = await connectionManager.GetOrCreateSftpClientAsync(connectionName, cancellationToken);
        if (sftpClient is null)
            return (false, false, CreateConnectionRecoveryMessage(connectionName));

        try
        {
            bool exists = await Task.Run(() => sftpClient.Exists(remotePath), cancellationToken);
            if (!exists)
                return (false, false, null);

            SftpFileAttributes attrs = await Task.Run(() => sftpClient.GetAttributes(remotePath), cancellationToken);
            return (true, attrs.IsDirectory, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check existence of {Path}", remotePath);
            return (false, false, ex.Message);
        }
    }

    private static string GetPermissionsString(ISftpFile file)
    {
        // Get permission bits from the file mode
        int mode = (int)(file.Attributes.OwnerCanRead ? 256 : 0)
                   | (int)(file.Attributes.OwnerCanWrite ? 128 : 0)
                   | (int)(file.Attributes.OwnerCanExecute ? 64 : 0)
                   | (int)(file.Attributes.GroupCanRead ? 32 : 0)
                   | (int)(file.Attributes.GroupCanWrite ? 16 : 0)
                   | (int)(file.Attributes.GroupCanExecute ? 8 : 0)
                   | (int)(file.Attributes.OthersCanRead ? 4 : 0)
                   | (int)(file.Attributes.OthersCanWrite ? 2 : 0)
                   | (int)(file.Attributes.OthersCanExecute ? 1 : 0);

        var sb = new System.Text.StringBuilder(10);

        sb.Append(file.IsDirectory ? 'd' : file.IsSymbolicLink ? 'l' : '-');
        sb.Append(file.Attributes.OwnerCanRead ? 'r' : '-');
        sb.Append(file.Attributes.OwnerCanWrite ? 'w' : '-');
        sb.Append(file.Attributes.OwnerCanExecute ? 'x' : '-');
        sb.Append(file.Attributes.GroupCanRead ? 'r' : '-');
        sb.Append(file.Attributes.GroupCanWrite ? 'w' : '-');
        sb.Append(file.Attributes.GroupCanExecute ? 'x' : '-');
        sb.Append(file.Attributes.OthersCanRead ? 'r' : '-');
        sb.Append(file.Attributes.OthersCanWrite ? 'w' : '-');
        sb.Append(file.Attributes.OthersCanExecute ? 'x' : '-');

        return sb.ToString();
    }

    private SftpListResult CreateListRecoveryResult(string connectionName, string remotePath)
    {
        bool hasProfile = connectionManager.HasProfile(connectionName);
        return new SftpListResult
        {
            Success = false,
            Path = remotePath,
            Error = CreateConnectionRecoveryMessage(connectionName),
            ConnectionName = connectionName,
            ErrorCode = hasProfile
                ? SshRecoveryCodes.ConnectionNotConnected
                : SshRecoveryCodes.NoMatchingProfile,
            Recoverable = true,
            Recovery = CreateRecoveryGuidance(connectionName, hasProfile)
        };
    }

    private SftpTransferResult CreateTransferRecoveryResult(string connectionName, string sourcePath, string destinationPath)
    {
        bool hasProfile = connectionManager.HasProfile(connectionName);
        return new SftpTransferResult
        {
            Success = false,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            Error = CreateConnectionRecoveryMessage(connectionName),
            ConnectionName = connectionName,
            ErrorCode = hasProfile
                ? SshRecoveryCodes.ConnectionNotConnected
                : SshRecoveryCodes.NoMatchingProfile,
            Recoverable = true,
            Recovery = CreateRecoveryGuidance(connectionName, hasProfile)
        };
    }

    private string CreateConnectionRecoveryMessage(string connectionName)
    {
        return connectionManager.HasProfile(connectionName)
            ? $"Connection '{connectionName}' is not active. A matching saved profile exists, but automatic reconnect did not succeed."
            : $"Connection '{connectionName}' is not active and no saved profile matched that name.";
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
