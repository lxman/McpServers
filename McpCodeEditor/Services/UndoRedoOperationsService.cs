using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for handling undo and redo operations on tracked changes
/// </summary>
public class UndoRedoOperationsService(
    IContentSnapshotService contentSnapshotService,
    IChangeRecordPersistenceService changeRecordPersistenceService) : IUndoRedoOperationsService
{
    /// <summary>
    /// Undo a specific change by ID
    /// </summary>
    public async Task<UndoRedoResult> UndoChangeAsync(string changeId)
    {
        try
        {
            // Get the change record
            ChangeRecord? changeRecord = await changeRecordPersistenceService.GetChangeDetailsAsync(changeId);
            if (changeRecord == null)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"Change {changeId} not found"
                };
            }

            if (changeRecord.IsUndone)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"Change {changeId} has already been undone"
                };
            }

            // Check if the file still exists
            if (!File.Exists(changeRecord.FilePath))
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"File {changeRecord.FilePath} no longer exists"
                };
            }

            // Get the original content from the snapshot using the injected service
            string? originalContent = await contentSnapshotService.GetOriginalContentFromSnapshotAsync(changeId);
            if (originalContent == null)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"Cannot undo: original content snapshot not found for change {changeId}"
                };
            }

            // Read current content for the undo operation tracking
            string currentContent = await File.ReadAllTextAsync(changeRecord.FilePath);

            // Restore the original content
            await File.WriteAllTextAsync(changeRecord.FilePath, originalContent);

            // We need a way to track this undo operation - this will need to be handled by the calling service
            // For now, let's create a placeholder undo change ID
            var undoChangeId = $"undo_{changeId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            // Mark the original change as undone
            await changeRecordPersistenceService.MarkChangeAsUndoneAsync(changeId, undoChangeId);

            return new UndoRedoResult
            {
                Success = true,
                Message = $"Successfully undid change {changeId}",
                ChangeId = changeId,
                UndoRedoChangeId = undoChangeId,
                FilePath = changeRecord.FilePath,
                Operation = $"undo_{changeRecord.Operation}",
                RequiresChangeTracking = true, // Signal that the calling service needs to track this operation
                UndoContent = new Dictionary<string, object>
                {
                    { "current_content", currentContent },
                    { "restored_content", originalContent },
                    { "original_change_id", changeId }
                }
            };
        }
        catch (Exception ex)
        {
            return new UndoRedoResult
            {
                Success = false,
                Message = $"Failed to undo change: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Redo a previously undone change
    /// </summary>
    public async Task<UndoRedoResult> RedoChangeAsync(string changeId)
    {
        try
        {
            // Get the change record
            ChangeRecord? changeRecord = await changeRecordPersistenceService.GetChangeDetailsAsync(changeId);
            if (changeRecord == null)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"Change {changeId} not found"
                };
            }

            if (!changeRecord.IsUndone)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"Change {changeId} has not been undone"
                };
            }

            // Check if the file still exists
            if (!File.Exists(changeRecord.FilePath))
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"File {changeRecord.FilePath} no longer exists"
                };
            }

            // Get the modified content from the snapshot using the injected service
            string? modifiedContent = await contentSnapshotService.GetModifiedContentFromSnapshotAsync(changeId);
            if (modifiedContent == null)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    Message = $"Cannot redo: modified content snapshot not found for change {changeId}"
                };
            }

            // Read current content for the redo operation tracking
            string currentContent = await File.ReadAllTextAsync(changeRecord.FilePath);

            // Restore the modified content
            await File.WriteAllTextAsync(changeRecord.FilePath, modifiedContent);

            // Create a placeholder redo change ID
            var redoChangeId = $"redo_{changeId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            // Mark the original change as not undone
            await changeRecordPersistenceService.MarkChangeAsRedoneAsync(changeId, redoChangeId);

            return new UndoRedoResult
            {
                Success = true,
                Message = $"Successfully redid change {changeId}",
                ChangeId = changeId,
                UndoRedoChangeId = redoChangeId,
                FilePath = changeRecord.FilePath,
                Operation = $"redo_{changeRecord.Operation}",
                RequiresChangeTracking = true, // Signal that the calling service needs to track this operation
                UndoContent = new Dictionary<string, object>
                {
                    { "current_content", currentContent },
                    { "restored_content", modifiedContent },
                    { "original_change_id", changeId }
                }
            };
        }
        catch (Exception ex)
        {
            return new UndoRedoResult
            {
                Success = false,
                Message = $"Failed to redo change: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Get list of changes that can be undone
    /// </summary>
    public async Task<List<ChangeRecord>> GetUndoableChangesAsync(int maxRecords = 20)
    {
        try
        {
            List<ChangeRecord> allChanges = await changeRecordPersistenceService.LoadChangeRecordsAsync();
            return allChanges
                .Where(c => !c.IsUndone && 
                           !c.Operation.StartsWith("undo_") && 
                           !c.Operation.StartsWith("redo_"))
                .OrderByDescending(c => c.Timestamp)
                .Take(maxRecords)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get undoable changes: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get list of changes that can be redone
    /// </summary>
    public async Task<List<ChangeRecord>> GetRedoableChangesAsync(int maxRecords = 20)
    {
        try
        {
            List<ChangeRecord> allChanges = await changeRecordPersistenceService.LoadChangeRecordsAsync();
            return allChanges
                .Where(c => c.IsUndone && 
                           !c.Operation.StartsWith("undo_") && 
                           !c.Operation.StartsWith("redo_"))
                .OrderByDescending(c => c.Timestamp)
                .Take(maxRecords)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get redoable changes: {ex.Message}", ex);
        }
    }
}
