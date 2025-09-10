namespace McpCodeEditor.Interfaces
{
    /// <summary>
    /// Service for managing content snapshots for change tracking operations
    /// </summary>
    public interface IContentSnapshotService
    {
        /// <summary>
        /// Save content snapshots for a change operation
        /// </summary>
        /// <param name="changeId">Unique identifier for the change</param>
        /// <param name="originalContent">Original content before change</param>
        /// <param name="modifiedContent">Modified content after change</param>
        Task SaveContentSnapshotAsync(string changeId, string originalContent, string modifiedContent);

        /// <summary>
        /// Retrieve original content from snapshot
        /// </summary>
        /// <param name="changeId">Change identifier</param>
        /// <returns>Original content or null if not found</returns>
        Task<string?> GetOriginalContentFromSnapshotAsync(string changeId);

        /// <summary>
        /// Retrieve modified content from snapshot
        /// </summary>
        /// <param name="changeId">Change identifier</param>
        /// <returns>Modified content or null if not found</returns>
        Task<string?> GetModifiedContentFromSnapshotAsync(string changeId);

        /// <summary>
        /// Delete content snapshot for a change
        /// </summary>
        /// <param name="changeId">Change identifier</param>
        Task DeleteContentSnapshotAsync(string changeId);

        /// <summary>
        /// Check if snapshot exists for a change
        /// </summary>
        /// <param name="changeId">Change identifier</param>
        /// <returns>True if snapshot exists, false otherwise</returns>
        Task<bool> SnapshotExistsAsync(string changeId);
    }
}
