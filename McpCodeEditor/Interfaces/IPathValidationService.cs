namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for path validation and resolution service.
/// Provides secure path handling operations for all refactoring services.
/// </summary>
public interface IPathValidationService
{
    /// <summary>
    /// Validates and resolves a path to an absolute path while ensuring security constraints.
    /// Converts relative paths to absolute paths and validates against security policies.
    /// </summary>
    /// <param name="path">The path to validate and resolve (can be relative or absolute)</param>
    /// <returns>The validated and resolved absolute path</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when path violates security constraints</exception>
    /// <exception cref="ArgumentException">Thrown when path is invalid or malformed</exception>
    string ValidateAndResolvePath(string path);

    /// <summary>
    /// Checks if a path is within the allowed workspace boundaries.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is within workspace boundaries, false otherwise</returns>
    bool IsPathWithinWorkspace(string path);

    /// <summary>
    /// Checks if a path is in the blocked paths list.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is blocked, false otherwise</returns>
    bool IsPathBlocked(string path);

    /// <summary>
    /// Validates that a file exists at the specified path after validation.
    /// </summary>
    /// <param name="path">The path to validate and check for file existence</param>
    /// <returns>The validated absolute path if file exists</returns>
    /// <exception cref="FileNotFoundException">Thrown when file does not exist</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when path violates security constraints</exception>
    string ValidateFileExists(string path);
}
