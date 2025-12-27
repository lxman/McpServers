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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
        {
            return new SftpListResult
            {
                Success = false,
                Path = remotePath,
                Error = $"Connection '{connectionName}' not found or not connected"
            };
        }

        try
        {
            logger.LogInformation("Listing {Path} on {Connection}", remotePath, connectionName);

            var items = await Task.Run(() => sftpClient.ListDirectory(remotePath).ToList(), cancellationToken);

            var files = items
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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
        {
            return new SftpTransferResult
            {
                Success = false,
                SourcePath = remotePath,
                DestinationPath = localPath,
                Error = $"Connection '{connectionName}' not found or not connected"
            };
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Downloading {Remote} to {Local}", remotePath, localPath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var fileStream = File.Create(localPath);
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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
        {
            return new SftpTransferResult
            {
                Success = false,
                SourcePath = localPath,
                DestinationPath = remotePath,
                Error = $"Connection '{connectionName}' not found or not connected"
            };
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
            await using var fileStream = File.OpenRead(localPath);
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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
            return (false, string.Empty, $"Connection '{connectionName}' not found or not connected");

        try
        {
            logger.LogInformation("Reading {Path} on {Connection}", remotePath, connectionName);

            var content = await Task.Run(() =>
            {
                using var stream = sftpClient.OpenRead(remotePath);
                using var reader = new StreamReader(stream);

                var buffer = new char[maxBytes];
                var read = reader.Read(buffer, 0, maxBytes);
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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
            return (false, $"Connection '{connectionName}' not found or not connected");

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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
            return (false, $"Connection '{connectionName}' not found or not connected");

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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
            return (false, $"Connection '{connectionName}' not found or not connected");

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
        var sftpClient = connectionManager.GetOrCreateSftpClient(connectionName);
        if (sftpClient is null)
            return (false, false, $"Connection '{connectionName}' not found or not connected");

        try
        {
            var exists = await Task.Run(() => sftpClient.Exists(remotePath), cancellationToken);
            if (!exists)
                return (false, false, null);

            var attrs = await Task.Run(() => sftpClient.GetAttributes(remotePath), cancellationToken);
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
        var mode = (int)(file.Attributes.OwnerCanRead ? 256 : 0)
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
}
