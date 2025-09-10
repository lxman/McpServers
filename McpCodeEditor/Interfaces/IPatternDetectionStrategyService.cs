using McpCodeEditor.Models;
using McpCodeEditor.Services;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for detecting different types of architecture patterns using various strategies
/// Phase 4 - Service Layer Cleanup: Pattern detection strategy extraction
/// </summary>
public interface IPatternDetectionStrategyService
{
    /// <summary>
    /// Detect patterns based on solution files and project combinations within solutions
    /// </summary>
    /// <param name="rootPath">Root directory path</param>
    /// <param name="projects">Available projects to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected solution-based patterns</returns>
    Task<List<ArchitecturePattern>> DetectSolutionBasedPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect directory-based patterns like monorepos and separated frontend/backend
    /// </summary>
    /// <param name="rootPath">Root directory path</param>
    /// <param name="projects">Available projects to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected directory-based patterns</returns>
    Task<List<ArchitecturePattern>> DetectDirectoryBasedPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect patterns based on naming conventions and project names
    /// </summary>
    /// <param name="rootPath">Root directory path</param>
    /// <param name="projects">Available projects to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected naming-based patterns</returns>
    Task<List<ArchitecturePattern>> DetectNamingBasedPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect patterns based on direct project type combinations
    /// </summary>
    /// <param name="rootPath">Root directory path</param>
    /// <param name="projects">Available projects to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected combination-based patterns</returns>
    Task<List<ArchitecturePattern>> DetectProjectCombinationPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect pattern from a specific combination of project types (used by solution analysis)
    /// </summary>
    /// <param name="projects">Projects to analyze</param>
    /// <param name="rootPath">Root path for the pattern</param>
    /// <returns>Detected pattern or null if no pattern matches</returns>
    ArchitecturePattern? DetectPatternFromProjectCombination(List<ProjectInfo> projects, string rootPath);
}
