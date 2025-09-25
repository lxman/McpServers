using System.ComponentModel;
using System.Text.Json;
using McpCodeEditor.Interfaces;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools;

[McpServerToolType]
public class BackupTools(
    CodeEditorConfigurationService config,
    IBackupService backupService)
{
    [McpServerTool]
    [Description("Create a backup of the workspace or specific directory")]
    public async Task<string> BackupCreateAsync(
        [Description("Path to backup (defaults to current workspace)")]
        string? sourcePath = null,
        [Description("Description for the backup")]
        string description = "Manual backup")
    {
        try
        {
            var pathToBackup = sourcePath ?? config.DefaultWorkspace;
            var backupId = await backupService.CreateBackupAsync(pathToBackup, description);

            var result = new
            {
                success = true,
                backup_id = backupId,
                message = $"Backup created successfully: {backupId}",
                source_path = pathToBackup
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("List backups for current workspace")]
    public async Task<string> BackupListAsync(
        [Description("Workspace path (defaults to current workspace)")]
        string? workspacePath = null)
    {
        try
        {
            var targetWorkspace = workspacePath ?? config.DefaultWorkspace;
            var backups = await backupService.ListBackupsAsync(targetWorkspace);
            
            var result = new
            {
                success = true,
                workspace_path = targetWorkspace,
                count = backups.Count,
                backups = backups.Select(b => new
                {
                    id = b.Id,
                    description = b.Description,
                    created_at = b.CreatedAt,
                    size_mb = Math.Round(b.SizeBytes / 1024.0 / 1024.0, 2),
                    files_count = b.Files.Count,
                    source_path = b.SourcePath,
                    workspace_hash = b.WorkspaceHash,
                    workspace_display_name = b.WorkspaceDisplayName
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("List all backups across all workspaces")]
    public async Task<string> BackupListAllAsync()
    {
        try
        {
            var allBackups = await backupService.ListAllBackupsAsync();
            var groupedByWorkspace = allBackups.GroupBy(b => new { b.WorkspaceHash, b.WorkspaceDisplayName })
                .Select(g => new
                {
                    workspace_hash = g.Key.WorkspaceHash,
                    workspace_display_name = g.Key.WorkspaceDisplayName,
                    workspace_path = g.First().WorkspacePath,
                    backup_count = g.Count(),
                    total_size_mb = Math.Round(g.Sum(b => b.SizeBytes) / 1024.0 / 1024.0, 2),
                    latest_backup = g.OrderByDescending(b => b.CreatedAt).First().CreatedAt,
                    backups = g.OrderByDescending(b => b.CreatedAt).Select(b => new
                    {
                        id = b.Id,
                        description = b.Description,
                        created_at = b.CreatedAt,
                        size_mb = Math.Round(b.SizeBytes / 1024.0 / 1024.0, 2),
                        files_count = b.Files.Count
                    }).ToArray()
                }).ToArray();

            var result = new
            {
                success = true,
                total_backups = allBackups.Count,
                workspaces_with_backups = groupedByWorkspace.Length,
                total_size_mb = Math.Round(allBackups.Sum(b => b.SizeBytes) / 1024.0 / 1024.0, 2),
                workspaces = groupedByWorkspace
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Restore a backup by ID")]
    public async Task<string> BackupRestoreAsync(
        [Description("Backup ID to restore")]
        string backupId,
        [Description("Workspace path (defaults to current workspace)")]
        string? workspacePath = null,
        [Description("Target path for restoration (optional)")]
        string? targetPath = null)
    {
        try
        {
            var sourceWorkspace = workspacePath ?? config.DefaultWorkspace;
            var restored = await backupService.RestoreBackupAsync(backupId, sourceWorkspace, targetPath);
            
            var result = new
            {
                success = restored,
                message = restored ? $"Backup {backupId} restored successfully" : "Backup restoration failed",
                backup_id = backupId,
                workspace_path = sourceWorkspace,
                target_path = targetPath
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Delete a backup by ID")]
    public async Task<string> BackupDeleteAsync(
        [Description("Backup ID to delete")]
        string backupId,
        [Description("Workspace path (defaults to current workspace)")]
        string? workspacePath = null)
    {
        try
        {
            var targetWorkspace = workspacePath ?? config.DefaultWorkspace;
            var deleted = await backupService.DeleteBackupAsync(backupId, targetWorkspace);
            
            var result = new
            {
                success = deleted,
                message = deleted ? $"Backup {backupId} deleted successfully" : "Backup not found",
                backup_id = backupId,
                workspace_path = targetWorkspace
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Get detailed information about a specific backup")]
    public async Task<string> BackupGetInfoAsync(
        [Description("Backup ID to get info for")]
        string backupId,
        [Description("Workspace path (defaults to current workspace)")]
        string? workspacePath = null)
    {
        try
        {
            var targetWorkspace = workspacePath ?? config.DefaultWorkspace;
            var backupInfo = await backupService.GetBackupInfoAsync(backupId, targetWorkspace);
            
            if (backupInfo == null)
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = $"Backup {backupId} not found in workspace" 
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var result = new
            {
                success = true,
                backup_info = new
                {
                    id = backupInfo.Id,
                    description = backupInfo.Description,
                    source_path = backupInfo.SourcePath,
                    backup_path = backupInfo.BackupPath,
                    created_at = backupInfo.CreatedAt,
                    size_bytes = backupInfo.SizeBytes,
                    size_mb = Math.Round(backupInfo.SizeBytes / 1024.0 / 1024.0, 2),
                    files_count = backupInfo.Files.Count,
                    workspace_hash = backupInfo.WorkspaceHash,
                    workspace_path = backupInfo.WorkspacePath,
                    workspace_display_name = backupInfo.WorkspaceDisplayName,
                    files = backupInfo.Files
                }
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Clean up old backups for a workspace")]
    public async Task<string> BackupCleanupAsync(
        [Description("Number of recent backups to keep")]
        int keepCount = 10,
        [Description("Workspace path (defaults to current workspace)")]
        string? workspacePath = null)
    {
        try
        {
            var targetWorkspace = workspacePath ?? config.DefaultWorkspace;
            var deletedCount = await backupService.CleanupOldBackupsAsync(targetWorkspace, keepCount);
            
            var result = new
            {
                success = true,
                message = $"Cleaned up {deletedCount} old backups, keeping {keepCount} most recent",
                deleted_count = deletedCount,
                kept_count = keepCount,
                workspace_path = targetWorkspace
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
