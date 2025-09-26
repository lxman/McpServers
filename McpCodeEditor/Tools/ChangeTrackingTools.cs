using System.ComponentModel;
using System.Text.Json;
using McpCodeEditor.Models;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools;

[McpServerToolType]
public class ChangeTrackingTools(ChangeTrackingService changeTrackingService)
{
    [McpServerTool]
    [Description("Get change history for a specific file")]
    public async Task<string> ChangeGetFileHistoryAsync(
        [Description("Path to the file")]
        string filePath,
        [Description("Maximum number of records to return")]
        int maxRecords = 50)
    {
        try
        {
            List<ChangeRecord> changes = await changeTrackingService.GetFileHistoryAsync(filePath, maxRecords);
            var result = new
            {
                success = true,
                file_path = filePath,
                change_count = changes.Count,
                changes = changes.Select(c => new
                {
                    id = c.Id,
                    operation = c.Operation,
                    timestamp = c.Timestamp,
                    description = c.Description,
                    backup_id = c.BackupId,
                    is_undone = c.IsUndone,
                    undo_change_id = c.UndoChangeId,
                    redo_change_id = c.RedoChangeId,
                    details = new
                    {
                        lines_added = c.Details.LinesAdded,
                        lines_removed = c.Details.LinesRemoved,
                        lines_modified = c.Details.LinesModified,
                        size_change = c.Details.ModifiedSize - c.Details.OriginalSize
                    }
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Get recent changes across all files")]
    public async Task<string> ChangeGetRecentAsync(
        [Description("Maximum number of records to return")]
        int maxRecords = 100,
        [Description("Time range in hours (optional)")]
        int? hoursBack = null)
    {
        try
        {
            TimeSpan? timeRange = hoursBack.HasValue ? TimeSpan.FromHours(hoursBack.Value) : null;
            List<ChangeRecord> changes = await changeTrackingService.GetRecentChangesAsync(maxRecords, timeRange);

            var result = new
            {
                success = true,
                time_range_hours = hoursBack,
                change_count = changes.Count,
                changes = changes.Select(c => new
                {
                    id = c.Id,
                    file_path = c.FilePath,
                    operation = c.Operation,
                    timestamp = c.Timestamp,
                    description = c.Description,
                    backup_id = c.BackupId,
                    is_undone = c.IsUndone,
                    undo_change_id = c.UndoChangeId,
                    redo_change_id = c.RedoChangeId
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Get change statistics and analytics")]
    public async Task<string> ChangeGetStatsAsync(
        [Description("Time range in hours (optional)")]
        int? hoursBack = null)
    {
        try
        {
            TimeSpan? timeRange = hoursBack.HasValue ? TimeSpan.FromHours(hoursBack.Value) : null;
            object stats = await changeTrackingService.GetChangeStatsAsync(timeRange);

            var result = new
            {
                success = true,
                time_range_hours = hoursBack,
                stats = stats
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Undo a specific change by ID")]
    public async Task<string> ChangesUndoAsync(
        [Description("Change ID to undo")]
        string changeId)
    {
        try
        {
            UndoRedoResult result = await changeTrackingService.UndoChangeAsync(changeId);

            var response = new
            {
                success = result.Success,
                message = result.Message,
                change_id = result.ChangeId,
                undo_change_id = result.UndoRedoChangeId,
                file_path = result.FilePath,
                operation = result.Operation
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Redo a previously undone change by ID")]
    public async Task<string> ChangesRedoAsync(
        [Description("Change ID to redo")]
        string changeId)
    {
        try
        {
            UndoRedoResult result = await changeTrackingService.RedoChangeAsync(changeId);

            var response = new
            {
                success = result.Success,
                message = result.Message,
                change_id = result.ChangeId,
                redo_change_id = result.UndoRedoChangeId,
                file_path = result.FilePath,
                operation = result.Operation
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Get list of changes that can be undone")]
    public async Task<string> ChangeGetUndoableAsync(
        [Description("Maximum number of records to return")]
        int maxRecords = 20)
    {
        try
        {
            List<ChangeRecord> changes = await changeTrackingService.GetUndoableChangesAsync(maxRecords);

            var result = new
            {
                success = true,
                undoable_count = changes.Count,
                changes = changes.Select(c => new
                {
                    id = c.Id,
                    file_path = c.FilePath,
                    operation = c.Operation,
                    timestamp = c.Timestamp,
                    description = c.Description,
                    details = new
                    {
                        lines_added = c.Details.LinesAdded,
                        lines_removed = c.Details.LinesRemoved,
                        lines_modified = c.Details.LinesModified,
                        size_change = c.Details.ModifiedSize - c.Details.OriginalSize
                    }
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Get list of changes that can be redone")]
    public async Task<string> ChangeGetRedoableAsync(
        [Description("Maximum number of records to return")]
        int maxRecords = 20)
    {
        try
        {
            List<ChangeRecord> changes = await changeTrackingService.GetRedoableChangesAsync(maxRecords);

            var result = new
            {
                success = true,
                redoable_count = changes.Count,
                changes = changes.Select(c => new
                {
                    id = c.Id,
                    file_path = c.FilePath,
                    operation = c.Operation,
                    timestamp = c.Timestamp,
                    description = c.Description,
                    details = new
                    {
                        lines_added = c.Details.LinesAdded,
                        lines_removed = c.Details.LinesRemoved,
                        lines_modified = c.Details.LinesModified,
                        size_change = c.Details.ModifiedSize - c.Details.OriginalSize
                    }
                }).ToArray()
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
