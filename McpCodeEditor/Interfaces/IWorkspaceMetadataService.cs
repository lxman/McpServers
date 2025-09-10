using McpCodeEditor.Models;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for managing workspace metadata including hash mappings and access tracking
/// </summary>
public interface IWorkspaceMetadataService
{
    /// <summary>
    /// Gets metadata for a workspace by its hash
    /// </summary>
    /// <param name="workspaceHash">16-character workspace hash</param>
    /// <returns>Workspace metadata or null if not found</returns>
    Task<WorkspaceMetadata?> GetWorkspaceMetadataAsync(string workspaceHash);

    /// <summary>
    /// Gets metadata for a workspace by its current path
    /// </summary>
    /// <param name="workspacePath">Current workspace path</param>
    /// <returns>Workspace metadata or null if not found</returns>
    Task<WorkspaceMetadata?> GetWorkspaceMetadataByPathAsync(string workspacePath);

    /// <summary>
    /// Creates or updates workspace metadata
    /// </summary>
    /// <param name="workspacePath">Workspace path</param>
    /// <param name="displayName">Optional display name (auto-generated if not provided)</param>
    /// <returns>Created or updated workspace metadata</returns>
    Task<WorkspaceMetadata> CreateOrUpdateWorkspaceMetadataAsync(string workspacePath, string? displayName = null);

    /// <summary>
    /// Updates the last accessed timestamp for a workspace
    /// </summary>
    /// <param name="workspacePath">Workspace path</param>
    /// <returns>Updated workspace metadata</returns>
    Task<WorkspaceMetadata> UpdateLastAccessedAsync(string workspacePath);

    /// <summary>
    /// Updates the current path for a workspace (for handling moves/renames)
    /// </summary>
    /// <param name="workspaceHash">16-character workspace hash</param>
    /// <param name="newPath">New workspace path</param>
    /// <returns>Updated workspace metadata or null if workspace not found</returns>
    Task<WorkspaceMetadata?> UpdateWorkspacePathAsync(string workspaceHash, string newPath);

    /// <summary>
    /// Gets all workspace metadata entries
    /// </summary>
    /// <returns>List of all workspace metadata</returns>
    Task<List<WorkspaceMetadata>> GetAllWorkspaceMetadataAsync();

    /// <summary>
    /// Gets recently accessed workspaces
    /// </summary>
    /// <param name="maxCount">Maximum number of workspaces to return</param>
    /// <returns>List of recently accessed workspace metadata, ordered by last accessed descending</returns>
    Task<List<WorkspaceMetadata>> GetRecentWorkspacesAsync(int maxCount = 10);

    /// <summary>
    /// Removes workspace metadata
    /// </summary>
    /// <param name="workspaceHash">16-character workspace hash</param>
    /// <returns>True if removed, false if not found</returns>
    Task<bool> RemoveWorkspaceMetadataAsync(string workspaceHash);

    /// <summary>
    /// Finds workspaces that haven't been accessed for a specified number of days
    /// </summary>
    /// <param name="daysUnused">Number of days without access</param>
    /// <returns>List of unused workspace metadata</returns>
    Task<List<WorkspaceMetadata>> GetUnusedWorkspacesAsync(int daysUnused = 90);

    /// <summary>
    /// Adds or updates tags for a workspace
    /// </summary>
    /// <param name="workspaceHash">16-character workspace hash</param>
    /// <param name="tags">Tags to set</param>
    /// <returns>Updated workspace metadata or null if not found</returns>
    Task<WorkspaceMetadata?> UpdateWorkspaceTagsAsync(string workspaceHash, List<string> tags);

    /// <summary>
    /// Updates notes for a workspace
    /// </summary>
    /// <param name="workspaceHash">16-character workspace hash</param>
    /// <param name="notes">Notes to set</param>
    /// <returns>Updated workspace metadata or null if not found</returns>
    Task<WorkspaceMetadata?> UpdateWorkspaceNotesAsync(string workspaceHash, string notes);

    /// <summary>
    /// Finds workspaces by display name (partial match)
    /// </summary>
    /// <param name="searchTerm">Search term for display name</param>
    /// <returns>List of matching workspace metadata</returns>
    Task<List<WorkspaceMetadata>> FindWorkspacesByNameAsync(string searchTerm);
}
