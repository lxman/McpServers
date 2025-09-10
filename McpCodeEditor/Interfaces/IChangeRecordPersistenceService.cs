using McpCodeEditor.Models;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for persisting and retrieving change records
/// </summary>
public interface IChangeRecordPersistenceService
{
    /// <summary>
    /// Load all change records from storage
    /// </summary>
    /// <returns>List of all change records</returns>
    Task<List<ChangeRecord>> LoadChangeRecordsAsync();

    /// <summary>
    /// Append a new change record to storage
    /// </summary>
    /// <param name="changeRecord">Change record to persist</param>
    Task AppendChangeRecordAsync(ChangeRecord changeRecord);

    /// <summary>
    /// Rewrite all change records to storage (used for cleanup operations)
    /// </summary>
    /// <param name="changes">List of change records to persist</param>
    Task RewriteChangeLogAsync(List<ChangeRecord> changes);

    /// <summary>
    /// Mark a change as undone
    /// </summary>
    /// <param name="changeId">ID of the change to mark as undone</param>
    /// <param name="undoChangeId">ID of the undo operation change</param>
    Task MarkChangeAsUndoneAsync(string changeId, string undoChangeId);

    /// <summary>
    /// Mark a change as redone (undoes the undo)
    /// </summary>
    /// <param name="changeId">ID of the change to mark as redone</param>
    /// <param name="redoChangeId">ID of the redo operation change</param>
    Task MarkChangeAsRedoneAsync(string changeId, string redoChangeId);

    /// <summary>
    /// Get detailed information about a specific change
    /// </summary>
    /// <param name="changeId">ID of the change to retrieve</param>
    /// <returns>Change record or null if not found</returns>
    Task<ChangeRecord?> GetChangeDetailsAsync(string changeId);
}
