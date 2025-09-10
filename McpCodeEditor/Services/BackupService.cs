using System.IO.Compression;
using McpCodeEditor.Models;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services;

public class BackupService(CodeEditorConfigurationService config, IAppDataPathService appDataPathService) : IBackupService
{
    private readonly CodeEditorConfigurationService _config = config;

    /// <summary>
    /// Create a backup of the specified directory or workspace
    /// </summary>
    public async Task<string> CreateBackupAsync(string sourcePath, string description = "")
    {
        try
        {
            // Generate unique backup ID - Fixed GUID formatting bug
            string guidPart = Guid.NewGuid().ToString("N")[..8];
            var backupId = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{guidPart}";
            
            // Get workspace-specific backup directory with date-based organization
            string workspaceBackupDir = appDataPathService.GetWorkspaceBackupsDirectory(sourcePath);
            string dateBasedSubDir = Path.Combine(workspaceBackupDir, 
                DateTime.UtcNow.ToString("yyyy"), 
                DateTime.UtcNow.ToString("MM"));
            
            // Ensure backup directory structure exists
            appDataPathService.EnsureDirectoryExists(dateBasedSubDir);
            
            string backupPath = Path.Combine(dateBasedSubDir, $"{backupId}.zip");

            // Get all files to backup (exclude certain directories and files)
            List<string> filesToBackup = GetFilesToBackup(sourcePath);

            if (filesToBackup.Count == 0)
            {
                throw new InvalidOperationException("No files found to backup");
            }

            // Create ZIP backup
            using (ZipArchive archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                foreach (string file in filesToBackup)
                {
                    string relativePath = Path.GetRelativePath(sourcePath, file);
                    archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
                }
            }

            // Create backup info file with enhanced workspace metadata
            var backupInfo = new BackupInfo
            {
                Id = backupId,
                Description = string.IsNullOrWhiteSpace(description) ? "Automatic backup" : description,
                SourcePath = sourcePath,
                BackupPath = backupPath,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = new FileInfo(backupPath).Length,
                Files = filesToBackup.Select(f => Path.GetRelativePath(sourcePath, f)).ToList(),
                
                // New workspace-specific metadata
                WorkspaceHash = appDataPathService.GetWorkspaceHash(sourcePath),
                WorkspacePath = sourcePath,
                WorkspaceDisplayName = Path.GetFileName(sourcePath.TrimEnd('\\', '/'))
            };

            string infoPath = Path.Combine(dateBasedSubDir, $"{backupId}.json");
            string infoJson = System.Text.Json.JsonSerializer.Serialize(backupInfo, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(infoPath, infoJson);

            return backupId;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Backup creation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Restore a backup by ID for a specific workspace
    /// </summary>
    public async Task<bool> RestoreBackupAsync(string backupId, string workspacePath, string? targetPath = null)
    {
        try
        {
            // Find backup in workspace-specific directory structure
            BackupInfo? backupInfo = await FindBackupInWorkspaceAsync(backupId, workspacePath);
            
            if (backupInfo == null)
            {
                throw new FileNotFoundException($"Backup {backupId} not found in workspace");
            }

            string restorePath = targetPath ?? backupInfo.SourcePath;

            // Create restore directory if it doesn't exist
            Directory.CreateDirectory(restorePath);

            // Extract backup
            ZipFile.ExtractToDirectory(backupInfo.BackupPath, restorePath, overwriteFiles: true);

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Backup restoration failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// List all available backups for a specific workspace
    /// </summary>
    public async Task<List<BackupInfo>> ListBackupsAsync(string workspacePath)
    {
        try
        {
            var backups = new List<BackupInfo>();
            string workspaceBackupDir = appDataPathService.GetWorkspaceBackupsDirectory(workspacePath);
            
            if (!Directory.Exists(workspaceBackupDir))
            {
                return backups;
            }

            // Scan through date-based directory structure (YYYY/MM/)
            string[] yearDirs = Directory.GetDirectories(workspaceBackupDir)
                .Where(d => Directory.Exists(d))
                .ToArray();

            foreach (string yearDir in yearDirs)
            {
                string[] monthDirs = Directory.GetDirectories(yearDir)
                    .Where(d => Directory.Exists(d))
                    .ToArray();

                foreach (string monthDir in monthDirs)
                {
                    string[] infoFiles = Directory.GetFiles(monthDir, "*.json");

                    foreach (string infoFile in infoFiles)
                    {
                        try
                        {
                            string infoJson = await File.ReadAllTextAsync(infoFile);
                            var backupInfo = System.Text.Json.JsonSerializer.Deserialize<BackupInfo>(infoJson);

                            if (backupInfo != null)
                            {
                                backups.Add(backupInfo);
                            }
                        }
                        catch
                        {
                            // Skip invalid backup info files
                            continue;
                        }
                    }
                }
            }

            return backups.OrderByDescending(b => b.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list backups: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// List all backups across all workspaces (global view)
    /// </summary>
    public async Task<List<BackupInfo>> ListAllBackupsAsync()
    {
        try
        {
            var allBackups = new List<BackupInfo>();
            string appDataRoot = appDataPathService.GetAppDataRoot();
            
            // Find all workspace directories
            var workspacePattern = "workspace_*";
            string[] workspaceDirs = Directory.GetDirectories(appDataRoot, workspacePattern)
                .Where(d => Directory.Exists(d))
                .ToArray();

            foreach (string workspaceDir in workspaceDirs)
            {
                string backupDir = Path.Combine(workspaceDir, "backups");
                if (!Directory.Exists(backupDir))
                    continue;

                // Scan through date-based directory structure
                string[] yearDirs = Directory.GetDirectories(backupDir)
                    .Where(d => Directory.Exists(d))
                    .ToArray();

                foreach (string yearDir in yearDirs)
                {
                    string[] monthDirs = Directory.GetDirectories(yearDir)
                        .Where(d => Directory.Exists(d))
                        .ToArray();

                    foreach (string monthDir in monthDirs)
                    {
                        string[] infoFiles = Directory.GetFiles(monthDir, "*.json");

                        foreach (string infoFile in infoFiles)
                        {
                            try
                            {
                                string infoJson = await File.ReadAllTextAsync(infoFile);
                                var backupInfo = System.Text.Json.JsonSerializer.Deserialize<BackupInfo>(infoJson);

                                if (backupInfo != null)
                                {
                                    allBackups.Add(backupInfo);
                                }
                            }
                            catch
                            {
                                // Skip invalid backup info files
                                continue;
                            }
                        }
                    }
                }
            }

            return allBackups.OrderByDescending(b => b.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list all backups: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Delete a backup by ID for a specific workspace
    /// </summary>
    public async Task<bool> DeleteBackupAsync(string backupId, string workspacePath)
    {
        try
        {
            BackupInfo? backupInfo = await FindBackupInWorkspaceAsync(backupId, workspacePath);
            
            if (backupInfo == null)
            {
                return false;
            }

            var deleted = false;

            if (File.Exists(backupInfo.BackupPath))
            {
                File.Delete(backupInfo.BackupPath);
                deleted = true;
            }

            // Delete info file (same directory as backup zip)
            string infoPath = Path.ChangeExtension(backupInfo.BackupPath, ".json");
            if (File.Exists(infoPath))
            {
                File.Delete(infoPath);
                deleted = true;
            }

            return deleted;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete backup: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get backup info by ID for a specific workspace
    /// </summary>
    public async Task<BackupInfo?> GetBackupInfoAsync(string backupId, string workspacePath)
    {
        return await FindBackupInWorkspaceAsync(backupId, workspacePath);
    }

    /// <summary>
    /// Clean up old backups for a workspace (keep only the most recent N backups)
    /// </summary>
    public async Task<int> CleanupOldBackupsAsync(string workspacePath, int keepCount = 10)
    {
        try
        {
            List<BackupInfo> backups = await ListBackupsAsync(workspacePath);
            List<BackupInfo> backupsToDelete = backups.Skip(keepCount).ToList();

            var deletedCount = 0;
            foreach (BackupInfo backup in backupsToDelete)
            {
                if (await DeleteBackupAsync(backup.Id, workspacePath))
                {
                    deletedCount++;
                }
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Backup cleanup failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Clean up old backups across all workspaces
    /// </summary>
    public async Task<int> CleanupAllOldBackupsAsync(int keepCount = 10)
    {
        try
        {
            List<BackupInfo> allBackups = await ListAllBackupsAsync();
            IEnumerable<IGrouping<string, BackupInfo>> backupsByWorkspace = allBackups.GroupBy(b => b.WorkspaceHash);
            
            var totalDeleted = 0;
            foreach (IGrouping<string, BackupInfo> workspaceGroup in backupsByWorkspace)
            {
                List<BackupInfo> orderedBackups = workspaceGroup.OrderByDescending(b => b.CreatedAt).ToList();
                List<BackupInfo> backupsToDelete = orderedBackups.Skip(keepCount).ToList();

                foreach (BackupInfo backup in backupsToDelete)
                {
                    if (await DeleteBackupAsync(backup.Id, backup.WorkspacePath))
                    {
                        totalDeleted++;
                    }
                }
            }

            return totalDeleted;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Global backup cleanup failed: {ex.Message}", ex);
        }
    }

    private async Task<BackupInfo?> FindBackupInWorkspaceAsync(string backupId, string workspacePath)
    {
        try
        {
            string workspaceBackupDir = appDataPathService.GetWorkspaceBackupsDirectory(workspacePath);
            
            if (!Directory.Exists(workspaceBackupDir))
            {
                return null;
            }

            // Search through date-based directory structure
            string[] yearDirs = Directory.GetDirectories(workspaceBackupDir)
                .Where(d => Directory.Exists(d))
                .ToArray();

            foreach (string yearDir in yearDirs)
            {
                string[] monthDirs = Directory.GetDirectories(yearDir)
                    .Where(d => Directory.Exists(d))
                    .ToArray();

                foreach (string monthDir in monthDirs)
                {
                    string infoPath = Path.Combine(monthDir, $"{backupId}.json");
                    
                    if (File.Exists(infoPath))
                    {
                        string infoJson = await File.ReadAllTextAsync(infoPath);
                        return System.Text.Json.JsonSerializer.Deserialize<BackupInfo>(infoJson);
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetFilesToBackup(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
        }

        var files = new List<string>();
        var excludeDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "node_modules", ".git", ".vs", ".vscode",
            "packages", "target", "build", ".mcp-backups", ".mcp-changes"
        };

        var excludeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".pdb", ".cache", ".tmp", ".log"
        };

        void ScanDirectory(string directory)
        {
            try
            {
                // Skip excluded directories
                string dirName = Path.GetFileName(directory);
                if (excludeDirectories.Contains(dirName))
                {
                    return;
                }

                // Add files
                foreach (string file in Directory.GetFiles(directory))
                {
                    string extension = Path.GetExtension(file);
                    if (!excludeExtensions.Contains(extension))
                    {
                        files.Add(file);
                    }
                }

                // Recursively scan subdirectories
                foreach (string subdirectory in Directory.GetDirectories(directory))
                {
                    ScanDirectory(subdirectory);
                }
            }
            catch
            {
                // Skip directories we can't access
            }
        }

        ScanDirectory(sourcePath);
        return files;
    }
}
