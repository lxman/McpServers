using McpCodeEditor.Models;

namespace McpCodeEditor.Interfaces;

public interface IChangeTrackingService
{
    /// <summary>
    /// Track a file change operation
    /// </summary>
    Task<string> TrackChangeAsync(
        string filePath,
        string originalContent,
        string modifiedContent,
        string operation,
        string? backupId = null,
        Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Undo a specific change by ID
    /// </summary>
    Task<UndoRedoResult> UndoChangeAsync(string changeId);

    /// <summary>
    /// Redo a previously undone change
    /// </summary>
    Task<UndoRedoResult> RedoChangeAsync(string changeId);

    /// <summary>
    /// Get list of changes that can be undone
    /// </summary>
    Task<List<ChangeRecord>> GetUndoableChangesAsync(int maxRecords = 20);

    /// <summary>
    /// Get list of changes that can be redone
    /// </summary>
    Task<List<ChangeRecord>> GetRedoableChangesAsync(int maxRecords = 20);

    /// <summary>
    /// Get change history for a specific file
    /// </summary>
    Task<List<ChangeRecord>> GetFileHistoryAsync(string filePath, int maxRecords = 50);

    /// <summary>
    /// Get recent changes across all files
    /// </summary>
    Task<List<ChangeRecord>> GetRecentChangesAsync(int maxRecords = 100, TimeSpan? timeRange = null);

    /// <summary>
    /// Get change statistics
    /// </summary>
    Task<object> GetChangeStatsAsync(TimeSpan? timeRange = null);

    /// <summary>
    /// Get detailed information about a specific change
    /// </summary>
    Task<ChangeRecord?> GetChangeDetailsAsync(string changeId);

    /// <summary>
    /// Cleanup old change records (keep only recent N records)
    /// </summary>
    Task<int> CleanupOldChangesAsync(int keepCount = 1000);

    /// <summary>
    /// Export change history to a file
    /// </summary>
    Task<string> ExportChangesAsync(string exportPath, TimeSpan? timeRange = null);
}
