using McpCodeEditor.Models;

namespace McpCodeEditor.Interfaces;

public interface IBackupService
{
    /// <summary>
    /// Create a backup of the specified directory or workspace
    /// </summary>
    Task<string> CreateBackupAsync(string sourcePath, string description = "");

    /// <summary>
    /// Restore a backup by ID for a specific workspace
    /// </summary>
    Task<bool> RestoreBackupAsync(string backupId, string workspacePath, string? targetPath = null);

    /// <summary>
    /// List all available backups for a specific workspace
    /// </summary>
    Task<List<BackupInfo>> ListBackupsAsync(string workspacePath);

    /// <summary>
    /// List all backups across all workspaces (global view)
    /// </summary>
    Task<List<BackupInfo>> ListAllBackupsAsync();

    /// <summary>
    /// Delete a backup by ID for a specific workspace
    /// </summary>
    Task<bool> DeleteBackupAsync(string backupId, string workspacePath);

    /// <summary>
    /// Get backup info by ID for a specific workspace
    /// </summary>
    Task<BackupInfo?> GetBackupInfoAsync(string backupId, string workspacePath);

    /// <summary>
    /// Clean up old backups for a workspace (keep only the most recent N backups)
    /// </summary>
    Task<int> CleanupOldBackupsAsync(string workspacePath, int keepCount = 10);

    /// <summary>
    /// Clean up old backups across all workspaces
    /// </summary>
    Task<int> CleanupAllOldBackupsAsync(int keepCount = 10);
}
