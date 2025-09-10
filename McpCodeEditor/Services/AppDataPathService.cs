using System.Security.Cryptography;
using System.Text;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services
{
    /// <summary>
    /// Service for managing application data paths in %APPDATA%\McpCodeEditor\
    /// Implements workspace-first organization with 16-character hash-based directories
    /// </summary>
    public class AppDataPathService : IAppDataPathService
    {
        private const string AppDataSubDirectory = "McpCodeEditor";
        private const int WorkspaceHashLength = 16;

        private readonly string _appDataRoot;

        public AppDataPathService()
        {
            // Get %APPDATA% path, fallback to current directory if not available
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appDataPath))
            {
                appDataPath = Environment.CurrentDirectory;
            }

            _appDataRoot = Path.Combine(appDataPath, AppDataSubDirectory);
        }

        public string GetAppDataRoot()
        {
            return _appDataRoot;
        }

        public string GetBackupsDirectory()
        {
            return Path.Combine(_appDataRoot, "global", "backups");
        }

        public string GetChangesDirectory()
        {
            return Path.Combine(_appDataRoot, "global", "changes");
        }

        public string GetConfigDirectory()
        {
            return Path.Combine(_appDataRoot, "config");
        }

        public string GetCacheDirectory()
        {
            return Path.Combine(_appDataRoot, "cache");
        }

        public string GetTempDirectory()
        {
            return Path.Combine(_appDataRoot, "temp");
        }

        public string GetWorkspaceHash(string workspacePath)
        {
            if (string.IsNullOrEmpty(workspacePath))
            {
                throw new ArgumentException("Workspace path cannot be null or empty", nameof(workspacePath));
            }

            string normalizedPath = NormalizeWorkspacePath(workspacePath);
            
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedPath));
                string hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
                
                // Return first 16 characters for collision resistance while keeping paths manageable
                return hashString[..WorkspaceHashLength];
            }
        }

        public string GetWorkspaceDirectory(string workspacePath)
        {
            string hash = GetWorkspaceHash(workspacePath);
            return Path.Combine(_appDataRoot, $"workspace_{hash}");
        }

        public string GetWorkspaceChangesFile(string workspacePath)
        {
            string workspaceDir = GetWorkspaceDirectory(workspacePath);
            return Path.Combine(workspaceDir, "changes.jsonl");
        }

        public string GetWorkspaceSnapshotsDirectory(string workspacePath)
        {
            string workspaceDir = GetWorkspaceDirectory(workspacePath);
            return Path.Combine(workspaceDir, "snapshots");
        }

        public string GetWorkspaceBackupsDirectory(string workspacePath)
        {
            string workspaceDir = GetWorkspaceDirectory(workspacePath);
            return Path.Combine(workspaceDir, "backups");
        }

        public string GetWorkspaceCacheDirectory(string workspacePath)
        {
            string workspaceDir = GetWorkspaceDirectory(workspacePath);
            return Path.Combine(workspaceDir, "cache");
        }

        public string GetWorkspaceTempDirectory(string workspacePath)
        {
            string workspaceDir = GetWorkspaceDirectory(workspacePath);
            return Path.Combine(workspaceDir, "temp");
        }

        public void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create directory: {path}", ex);
            }
        }

        public string NormalizeWorkspacePath(string workspacePath)
        {
            if (string.IsNullOrEmpty(workspacePath))
            {
                throw new ArgumentException("Workspace path cannot be null or empty", nameof(workspacePath));
            }

            try
            {
                // Convert to absolute path
                string absolutePath = Path.GetFullPath(workspacePath);
                
                // Convert to lowercase for consistent hashing
                string lowerPath = absolutePath.ToLowerInvariant();
                
                // Replace backslashes with forward slashes for cross-platform consistency
                string forwardSlashPath = lowerPath.Replace('\\', '/');
                
                // Remove trailing slashes
                string trimmedPath = forwardSlashPath.TrimEnd('/');
                
                // Handle UNC paths: preserve //server/ prefix
                if (trimmedPath.StartsWith("//"))
                {
                    // UNC path - keep as is after normalization
                    return trimmedPath;
                }
                
                // Handle Windows drive letters: ensure consistent format
                if (trimmedPath is [_, ':', ..])
                {
                    // Windows drive path - normalize drive letter
                    return trimmedPath;
                }
                
                return trimmedPath;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid workspace path: {workspacePath}", nameof(workspacePath), ex);
            }
        }
    }
}
