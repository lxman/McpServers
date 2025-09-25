using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services.Security;

/// <summary>
/// Service for validating and resolving file paths with security constraints.
/// Implements path validation, resolution, and security checking for all refactoring operations.
/// </summary>
public class PathValidationService : IPathValidationService
{
    private readonly CodeEditorConfigurationService _config;

    /// <summary>
    /// Initializes a new instance of the PathValidationService.
    /// </summary>
    /// <param name="config">Configuration service containing security settings</param>
    public PathValidationService(CodeEditorConfigurationService config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Validates and resolves a path to an absolute path while ensuring security constraints.
    /// Converts relative paths to absolute paths and validates against security policies.
    /// </summary>
    /// <param name="path">The path to validate and resolve (can be relative or absolute)</param>
    /// <returns>The validated and resolved absolute path</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when path violates security constraints</exception>
    /// <exception cref="ArgumentException">Thrown when path is invalid or malformed</exception>
    public string ValidateAndResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        try
        {
            // Convert to an absolute path
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_config.DefaultWorkspace, path);
            fullPath = Path.GetFullPath(fullPath);

            // Security check: ensure path is within workspace if restricted
            if (_config.Security.RestrictToWorkspace)
            {
                if (!IsPathWithinWorkspace(fullPath))
                {
                    throw new UnauthorizedAccessException($"Access denied: Path outside workspace: {path}");
                }
            }

            // Check blocked paths
            if (IsPathBlocked(fullPath))
            {
                throw new UnauthorizedAccessException($"Access denied: Blocked path: {path}");
            }

            return fullPath;
        }
        catch (UnauthorizedAccessException)
        {
            throw; // Re-throw security exceptions as-is
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid path: {path}", nameof(path), ex);
        }
    }

    /// <summary>
    /// Checks if a path is within the allowed workspace boundaries.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is within workspace boundaries, false otherwise</returns>
    public bool IsPathWithinWorkspace(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var workspaceFullPath = Path.GetFullPath(_config.DefaultWorkspace);
            
            return fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If we can't determine the path relationship, assume it's not within workspace for security
            return false;
        }
    }

    /// <summary>
    /// Checks if a path is in the blocked paths list.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is blocked, false otherwise</returns>
    public bool IsPathBlocked(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            
            foreach (var blockedPath in _config.Security.BlockedPaths)
            {
                var blockedFullPath = Path.GetFullPath(blockedPath);
                if (fullPath.StartsWith(blockedFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If we can't determine the path relationship, assume it's blocked for security
            return true;
        }
    }

    /// <summary>
    /// Validates that a file exists at the specified path after validation.
    /// </summary>
    /// <param name="path">The path to validate and check for file existence</param>
    /// <returns>The validated absolute path if file exists</returns>
    /// <exception cref="FileNotFoundException">Thrown when file does not exist</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when path violates security constraints</exception>
    public string ValidateFileExists(string path)
    {
        // First validate and resolve the path
        var validatedPath = ValidateAndResolvePath(path);

        // Check if file exists
        if (!File.Exists(validatedPath))
        {
            throw new FileNotFoundException($"File not found: {path}", validatedPath);
        }

        return validatedPath;
    }
}
