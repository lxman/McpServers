using McpCodeEditor.Models;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for handling undo and redo operations on tracked changes
/// </summary>
public interface IUndoRedoOperationsService
{
    /// <summary>
    /// Undo a specific change by ID
    /// </summary>
    /// <param name="changeId">The ID of the change to undo</param>
    /// <returns>Result of the undo operation</returns>
    Task<UndoRedoResult> UndoChangeAsync(string changeId);

    /// <summary>
    /// Redo a previously undone change
    /// </summary>
    /// <param name="changeId">The ID of the change to redo</param>
    /// <returns>Result of the redo operation</returns>
    Task<UndoRedoResult> RedoChangeAsync(string changeId);

    /// <summary>
    /// Get list of changes that can be undone
    /// </summary>
    /// <param name="maxRecords">Maximum number of records to return</param>
    /// <returns>List of undoable changes</returns>
    Task<List<ChangeRecord>> GetUndoableChangesAsync(int maxRecords = 20);

    /// <summary>
    /// Get list of changes that can be redone
    /// </summary>
    /// <param name="maxRecords">Maximum number of records to return</param>
    /// <returns>List of redoable changes</returns>
    Task<List<ChangeRecord>> GetRedoableChangesAsync(int maxRecords = 20);
}
