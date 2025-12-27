namespace SshClient.Core.Models;

/// <summary>
/// Result of a file transfer operation
/// </summary>
public record SftpTransferResult
{
    /// <summary>
    /// Whether the transfer completed successfully
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Source path (local for upload, remote for download)
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Destination path (remote for upload, local for download)
    /// </summary>
    public string DestinationPath { get; init; } = string.Empty;

    /// <summary>
    /// Number of bytes transferred
    /// </summary>
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Duration of the transfer
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Transfer speed in bytes per second
    /// </summary>
    public double BytesPerSecond => Duration.TotalSeconds > 0 
        ? BytesTransferred / Duration.TotalSeconds 
        : 0;

    /// <summary>
    /// Error message if transfer failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Connection name used for transfer
    /// </summary>
    public string ConnectionName { get; init; } = string.Empty;
}

/// <summary>
/// Information about a remote file or directory
/// </summary>
public record SftpFileInfo
{
    /// <summary>
    /// Full path on remote system
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// File or directory name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this is a directory
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File size in bytes (0 for directories)
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Last modified time
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    /// Last access time
    /// </summary>
    public DateTime LastAccess { get; init; }

    /// <summary>
    /// Unix permissions string (e.g., "-rwxr-xr-x")
    /// </summary>
    public string? Permissions { get; init; }

    /// <summary>
    /// Owner user ID
    /// </summary>
    public int? OwnerId { get; init; }

    /// <summary>
    /// Group ID
    /// </summary>
    public int? GroupId { get; init; }
}

/// <summary>
/// Result of a directory listing operation
/// </summary>
public record SftpListResult
{
    /// <summary>
    /// Whether the listing succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Path that was listed
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Files and directories in the path
    /// </summary>
    public IReadOnlyList<SftpFileInfo> Items { get; init; } = [];

    /// <summary>
    /// Total number of items
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Number of directories
    /// </summary>
    public int DirectoryCount { get; init; }

    /// <summary>
    /// Number of files
    /// </summary>
    public int FileCount { get; init; }

    /// <summary>
    /// Error message if listing failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Connection name used
    /// </summary>
    public string ConnectionName { get; init; } = string.Empty;
}
