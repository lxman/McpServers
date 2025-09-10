using McpCodeEditor.Services;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for discovering projects in directories and solution files
/// </summary>
public interface IProjectDiscoveryService
{
    /// <summary>
    /// Get detailed project information for all projects in a directory tree
    /// </summary>
    /// <param name="rootPath">Root directory to search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of discovered projects</returns>
    Task<List<ProjectInfo>> GetProjectsInDirectoryAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract project information from a solution file
    /// </summary>
    /// <param name="solutionFile">Path to solution file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects referenced in the solution</returns>
    Task<List<ProjectInfo>> ExtractProjectsFromSolutionAsync(string solutionFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a directory should be excluded from project scanning
    /// </summary>
    /// <param name="directory">Directory path to check</param>
    /// <returns>True if directory should be excluded</returns>
    bool IsExcludedDirectory(string directory);

    /// <summary>
    /// Check if directory seems like a project container that should be scanned more deeply
    /// </summary>
    /// <param name="directory">Directory path to check</param>
    /// <returns>True if directory likely contains sub-projects</returns>
    bool SeemsLikeProjectContainer(string directory);
}
