using Microsoft.CodeAnalysis;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service responsible for managing Roslyn workspaces (MSBuild and fallback modes)
/// Extracted from SymbolNavigationService - Phase 4 Task 2a
/// </summary>
public interface IWorkspaceManagementService : IDisposable
{
    /// <summary>
    /// Current solution loaded in the workspace
    /// </summary>
    Solution? CurrentSolution { get; }
    
    /// <summary>
    /// Whether the workspace is using fallback mode (AdhocWorkspace instead of MSBuildWorkspace)
    /// </summary>
    bool IsUsingFallbackWorkspace { get; }
    
    /// <summary>
    /// Whether the developer environment was successfully initialized
    /// </summary>
    bool IsEnvironmentInitialized { get; }
    
    /// <summary>
    /// Dictionary of loaded projects by path/name
    /// </summary>
    IReadOnlyDictionary<string, Project> ProjectCache { get; }
    
    /// <summary>
    /// Initialize or refresh the workspace for symbol navigation
    /// Handles both MSBuild and fallback workspace creation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if workspace was successfully created and loaded</returns>
    Task<bool> RefreshWorkspaceAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get environment status for diagnostics
    /// </summary>
    /// <returns>Dictionary of environment status information</returns>
    Dictionary<string, string> GetEnvironmentStatus();
}
