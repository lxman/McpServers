namespace McpCodeEditor.Interfaces
{
    /// <summary>
    /// Service for managing application data paths in %APPDATA%\McpCodeEditor\
    /// </summary>
    public interface IAppDataPathService
    {
        /// <summary>
        /// Gets the root directory for all MCP Code Editor data: %APPDATA%\McpCodeEditor\
        /// </summary>
        string GetAppDataRoot();

        /// <summary>
        /// Gets the global backups directory: %APPDATA%\McpCodeEditor\global\backups\
        /// </summary>
        string GetBackupsDirectory();

        /// <summary>
        /// Gets the global changes directory: %APPDATA%\McpCodeEditor\global\changes\
        /// </summary>
        string GetChangesDirectory();

        /// <summary>
        /// Gets the global configuration directory: %APPDATA%\McpCodeEditor\config\
        /// </summary>
        string GetConfigDirectory();

        /// <summary>
        /// Gets the global cache directory: %APPDATA%\McpCodeEditor\cache\
        /// </summary>
        string GetCacheDirectory();

        /// <summary>
        /// Gets the global temp directory: %APPDATA%\McpCodeEditor\temp\
        /// </summary>
        string GetTempDirectory();

        /// <summary>
        /// Gets a 16-character hash for the given workspace path for directory organization
        /// </summary>
        /// <param name="workspacePath">Full path to workspace directory</param>
        /// <returns>16-character hash for consistent workspace identification</returns>
        string GetWorkspaceHash(string workspacePath);

        /// <summary>
        /// Gets the workspace-specific directory: %APPDATA%\McpCodeEditor\workspace_{hash}\
        /// </summary>
        /// <param name="workspacePath">Full path to workspace directory</param>
        /// <returns>Workspace-specific directory path</returns>
        string GetWorkspaceDirectory(string workspacePath);

        /// <summary>
        /// Gets the workspace-specific changes file: %APPDATA%\McpCodeEditor\workspace_{hash}\changes.jsonl
        /// </summary>
        /// <param name="workspacePath">Full path to workspace directory</param>
        /// <returns>Path to workspace-specific change tracking file</returns>
        string GetWorkspaceChangesFile(string workspacePath);

        /// <summary>
        /// Gets the workspace-specific snapshots directory: %APPDATA%\McpCodeEditor\workspace_{hash}\snapshots\
        /// </summary>
        /// <param name="workspacePath">Full path to workspace directory</param>
        /// <returns>Path to workspace-specific snapshots directory</returns>
        string GetWorkspaceSnapshotsDirectory(string workspacePath);

        /// <summary>
        /// Gets the workspace-specific backups directory: %APPDATA%\McpCodeEditor\workspace_{hash}\backups\
        /// </summary>
        /// <param name="workspacePath">Full path to workspace directory</param>
        /// <returns>Path to workspace-specific backups directory</returns>
        string GetWorkspaceBackupsDirectory(string workspacePath);

        /// <summary>
        /// Gets the workspace-specific cache directory: %APPDATA%\McpCodeEditor\workspace_{hash}\cache\
        /// </summary>
        /// <param name="workspacePath">Full path to workspace directory</param>
        /// <returns>Path to workspace-specific cache directory</returns>
        string GetWorkspaceCacheDirectory(string workspacePath);

        /// <summary>
        /// Gets the workspace-specific temp directory: %APPDATA%\McpCodeEditor\workspace_{hash}\temp\
        /// </summary>
        /// <param name="workspacePath">Full path to workspace directory</param>
        /// <returns>Path to workspace-specific temp directory</returns>
        string GetWorkspaceTempDirectory(string workspacePath);

        /// <summary>
        /// Ensures the specified directory exists, creating it if necessary
        /// </summary>
        /// <param name="path">Directory path to ensure exists</param>
        void EnsureDirectoryExists(string path);

        /// <summary>
        /// Normalizes a workspace path for consistent hashing
        /// </summary>
        /// <param name="workspacePath">Raw workspace path</param>
        /// <returns>Normalized path for hashing</returns>
        string NormalizeWorkspacePath(string workspacePath);
    }
}
