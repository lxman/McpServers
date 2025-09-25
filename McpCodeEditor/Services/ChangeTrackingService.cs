using System.Text.Json;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

public class ChangeTrackingService(
    CodeEditorConfigurationService config,
    IAppDataPathService appDataPathService,
    IContentSnapshotService contentSnapshotService,
    IChangeRecordPersistenceService changeRecordPersistenceService,
    IUndoRedoOperationsService undoRedoOperationsService,
    IChangeStatisticsService changeStatisticsService,
    IWorkspaceMetadataService? workspaceMetadataService = null) : IChangeTrackingService
{
    /// <summary>
    /// Gets the current workspace path from configuration
    /// </summary>
    private string CurrentWorkspacePath => config.DefaultWorkspace;

    /// <summary>
    /// Track a file change operation
    /// </summary>
    public async Task<string> TrackChangeAsync(
        string filePath,
        string originalContent,
        string modifiedContent,
        string operation,
        string? backupId = null,
        Dictionary<string, object>? metadata = null)
    {
        try
        {
            // Fixed GUID formatting bug
            var guidPart = Guid.NewGuid().ToString("N")[..8];
            var changeId = $"change_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{guidPart}";

            var changeRecord = new ChangeRecord
            {
                Id = changeId,
                FilePath = Path.GetFullPath(filePath),
                Operation = operation,
                Timestamp = DateTime.UtcNow,
                BackupId = backupId,
                Description = operation,
                Details = CalculateChangeDetails(originalContent, modifiedContent, metadata)
            };

            // Save the change record using the injected persistence service
            await changeRecordPersistenceService.AppendChangeRecordAsync(changeRecord);

            // Optionally save content snapshots for detailed diff analysis
            if (config.Workspace.PreferredWorkspace != null) // Use this as a feature flag
            {
                await contentSnapshotService.SaveContentSnapshotAsync(changeId, originalContent, modifiedContent);
            }

            return changeId;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to track change: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Undo a specific change by ID - delegated to UndoRedoOperationsService
    /// </summary>
    public async Task<UndoRedoResult> UndoChangeAsync(string changeId)
    {
        try
        {
            var result = await undoRedoOperationsService.UndoChangeAsync(changeId);
            
            // If the undo operation requires change tracking, track it
            if (result is { Success: true, RequiresChangeTracking: true, UndoContent: not null })
            {
                var currentContent = result.UndoContent["current_content"].ToString() ?? "";
                var restoredContent = result.UndoContent["restored_content"].ToString() ?? "";
                var originalChangeId = result.UndoContent["original_change_id"].ToString() ?? "";
                
                // Track the undo operation
                var undoChangeId = await TrackChangeAsync(
                    result.FilePath!,
                    currentContent,
                    restoredContent,
                    result.Operation!,
                    metadata: new Dictionary<string, object>
                    {
                        { "original_change_id", originalChangeId },
                        { "undo_operation", true }
                    });
                
                // Update the result with the tracked change ID
                result.UndoRedoChangeId = undoChangeId;
            }
            
            return result;
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
    /// Redo a previously undone change - delegated to UndoRedoOperationsService
    /// </summary>
    public async Task<UndoRedoResult> RedoChangeAsync(string changeId)
    {
        try
        {
            var result = await undoRedoOperationsService.RedoChangeAsync(changeId);
            
            // If the redo operation requires change tracking, track it
            if (result is { Success: true, RequiresChangeTracking: true, UndoContent: not null })
            {
                var currentContent = result.UndoContent["current_content"].ToString() ?? "";
                var restoredContent = result.UndoContent["restored_content"].ToString() ?? "";
                var originalChangeId = result.UndoContent["original_change_id"].ToString() ?? "";
                
                // Track the redo operation
                var redoChangeId = await TrackChangeAsync(
                    result.FilePath!,
                    currentContent,
                    restoredContent,
                    result.Operation!,
                    metadata: new Dictionary<string, object>
                    {
                        { "original_change_id", originalChangeId },
                        { "redo_operation", true }
                    });
                
                // Update the result with the tracked change ID
                result.UndoRedoChangeId = redoChangeId;
            }
            
            return result;
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
    /// Get list of changes that can be undone - delegated to UndoRedoOperationsService
    /// </summary>
    public async Task<List<ChangeRecord>> GetUndoableChangesAsync(int maxRecords = 20)
    {
        return await undoRedoOperationsService.GetUndoableChangesAsync(maxRecords);
    }

    /// <summary>
    /// Get list of changes that can be redone - delegated to UndoRedoOperationsService
    /// </summary>
    public async Task<List<ChangeRecord>> GetRedoableChangesAsync(int maxRecords = 20)
    {
        return await undoRedoOperationsService.GetRedoableChangesAsync(maxRecords);
    }

    /// <summary>
    /// Get change history for a specific file
    /// </summary>
    public async Task<List<ChangeRecord>> GetFileHistoryAsync(string filePath, int maxRecords = 50)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var allChanges = await changeRecordPersistenceService.LoadChangeRecordsAsync();

            return allChanges
                .Where(c => c.FilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.Timestamp)
                .Take(maxRecords)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get file history: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get recent changes across all files
    /// </summary>
    public async Task<List<ChangeRecord>> GetRecentChangesAsync(int maxRecords = 100, TimeSpan? timeRange = null)
    {
        try
        {
            var allChanges = await changeRecordPersistenceService.LoadChangeRecordsAsync();
            var query = allChanges.OrderByDescending(c => c.Timestamp);

            if (timeRange.HasValue)
            {
                var cutoffTime = DateTime.UtcNow - timeRange.Value;
                query = query.Where(c => c.Timestamp >= cutoffTime).OrderByDescending(c => c.Timestamp);
            }

            return query.Take(maxRecords).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get recent changes: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get change statistics - delegated to ChangeStatisticsService
    /// </summary>
    public async Task<object> GetChangeStatsAsync(TimeSpan? timeRange = null)
    {
        return await changeStatisticsService.GetChangeStatsAsync(timeRange);
    }

    /// <summary>
    /// Get detailed information about a specific change - delegated to ChangeRecordPersistenceService
    /// </summary>
    public async Task<ChangeRecord?> GetChangeDetailsAsync(string changeId)
    {
        return await changeRecordPersistenceService.GetChangeDetailsAsync(changeId);
    }

    /// <summary>
    /// Cleanup old change records (keep only recent N records)
    /// </summary>
    public async Task<int> CleanupOldChangesAsync(int keepCount = 1000)
    {
        try
        {
            var allChanges = await changeRecordPersistenceService.LoadChangeRecordsAsync();
            var changesToKeep = allChanges
                .OrderByDescending(c => c.Timestamp)
                .Take(keepCount)
                .ToList();

            // Rewrite the change log with only the records to keep
            await changeRecordPersistenceService.RewriteChangeLogAsync(changesToKeep);

            // Cleanup content snapshots for deleted changes using the injected service
            var deletedChanges = allChanges.Except(changesToKeep).ToList();
            foreach (var change in deletedChanges)
            {
                await contentSnapshotService.DeleteContentSnapshotAsync(change.Id);
            }

            return allChanges.Count - changesToKeep.Count;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to cleanup old changes: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Export change history to a file
    /// </summary>
    public async Task<string> ExportChangesAsync(string exportPath, TimeSpan? timeRange = null)
    {
        try
        {
            var changes = await GetRecentChangesAsync(10000, timeRange);

            var exportData = new
            {
                export_timestamp = DateTime.UtcNow,
                workspace = CurrentWorkspacePath,
                workspace_hash = appDataPathService.GetWorkspaceHash(CurrentWorkspacePath),
                data_location = appDataPathService.GetWorkspaceDirectory(CurrentWorkspacePath),
                total_changes = changes.Count,
                time_range = timeRange?.ToString(),
                changes = changes
            };

            var exportJson = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(exportPath, exportJson);
            return exportPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to export changes: {ex.Message}", ex);
        }
    }

    private static ChangeDetails CalculateChangeDetails(string originalContent, string modifiedContent, Dictionary<string, object>? metadata)
    {
        var originalLines = originalContent.Split('\n');
        var modifiedLines = modifiedContent.Split('\n');

        // Simple line-based diff calculation
        var details = new ChangeDetails
        {
            OriginalSize = originalContent.Length,
            ModifiedSize = modifiedContent.Length,
            ContentHash = ComputeHash(modifiedContent),
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        // Calculate line changes (simplified)
        if (modifiedLines.Length > originalLines.Length)
        {
            details.LinesAdded = modifiedLines.Length - originalLines.Length;
        }
        else if (modifiedLines.Length < originalLines.Length)
        {
            details.LinesRemoved = originalLines.Length - modifiedLines.Length;
        }

        // Count modified lines (simplified - just check if content is different)
        var minLines = Math.Min(originalLines.Length, modifiedLines.Length);
        details.LinesModified = 0;
        for (var i = 0; i < minLines; i++)
        {
            if (originalLines[i] != modifiedLines[i])
            {
                details.LinesModified++;
            }
        }

        return details;
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash)[..16]; // First 16 characters
    }
}
