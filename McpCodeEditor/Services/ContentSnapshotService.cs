using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services
{
    /// <summary>
    /// Service responsible for managing content snapshots for change tracking operations.
    /// Handles saving, retrieving, and deleting original/modified content snapshots.
    /// </summary>
    public class ContentSnapshotService : IContentSnapshotService
    {
        private readonly IAppDataPathService _appDataPathService;
        private readonly CodeEditorConfigurationService _config;

        public ContentSnapshotService(
            IAppDataPathService appDataPathService,
            CodeEditorConfigurationService config)
        {
            _appDataPathService = appDataPathService;
            _config = config;
        }

        /// <summary>
        /// Gets the workspace-specific snapshots directory
        /// </summary>
        private string SnapshotsDirectory
        {
            get
            {
                string directory = _appDataPathService.GetWorkspaceSnapshotsDirectory(_config.DefaultWorkspace);
                _appDataPathService.EnsureDirectoryExists(directory);
                return directory;
            }
        }

        /// <summary>
        /// Save content snapshots for a change operation
        /// </summary>
        public async Task SaveContentSnapshotAsync(string changeId, string originalContent, string modifiedContent)
        {
            try
            {
                string snapshotDir = Path.Combine(SnapshotsDirectory, changeId);
                _appDataPathService.EnsureDirectoryExists(snapshotDir);

                await File.WriteAllTextAsync(Path.Combine(snapshotDir, "original.txt"), originalContent);
                await File.WriteAllTextAsync(Path.Combine(snapshotDir, "modified.txt"), modifiedContent);
            }
            catch
            {
                // Non-critical operation - don't throw
                // Snapshots are optional for enhanced functionality
            }
        }

        /// <summary>
        /// Retrieve original content from snapshot
        /// </summary>
        public async Task<string?> GetOriginalContentFromSnapshotAsync(string changeId)
        {
            try
            {
                string snapshotDir = Path.Combine(SnapshotsDirectory, changeId);
                string originalFile = Path.Combine(snapshotDir, "original.txt");

                if (File.Exists(originalFile))
                {
                    return await File.ReadAllTextAsync(originalFile);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve modified content from snapshot
        /// </summary>
        public async Task<string?> GetModifiedContentFromSnapshotAsync(string changeId)
        {
            try
            {
                string snapshotDir = Path.Combine(SnapshotsDirectory, changeId);
                string modifiedFile = Path.Combine(snapshotDir, "modified.txt");

                if (File.Exists(modifiedFile))
                {
                    return await File.ReadAllTextAsync(modifiedFile);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Delete content snapshot for a change
        /// </summary>
        public async Task DeleteContentSnapshotAsync(string changeId)
        {
            try
            {
                string snapshotDir = Path.Combine(SnapshotsDirectory, changeId);
                if (Directory.Exists(snapshotDir))
                {
                    Directory.Delete(snapshotDir, recursive: true);
                }
            }
            catch
            {
                // Non-critical operation - don't throw
            }
        }

        /// <summary>
        /// Check if snapshot exists for a change
        /// </summary>
        public async Task<bool> SnapshotExistsAsync(string changeId)
        {
            try
            {
                string snapshotDir = Path.Combine(SnapshotsDirectory, changeId);
                string originalFile = Path.Combine(snapshotDir, "original.txt");
                string modifiedFile = Path.Combine(snapshotDir, "modified.txt");

                return File.Exists(originalFile) && File.Exists(modifiedFile);
            }
            catch
            {
                return false;
            }
        }
    }
}
