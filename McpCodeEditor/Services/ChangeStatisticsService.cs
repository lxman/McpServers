using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for generating change tracking statistics and analytics
/// </summary>
public class ChangeStatisticsService(
    IChangeRecordPersistenceService changeRecordPersistenceService,
    IUndoRedoOperationsService undoRedoOperationsService,
    CodeEditorConfigurationService config,
    IAppDataPathService appDataPathService) : IChangeStatisticsService
{
    /// <summary>
    /// Gets the current workspace path from configuration
    /// </summary>
    private string CurrentWorkspacePath => config.DefaultWorkspace;

    /// <summary>
    /// Get comprehensive change statistics
    /// </summary>
    public async Task<object> GetChangeStatsAsync(TimeSpan? timeRange = null)
    {
        try
        {
            // Load recent changes based on time range
            var changes = await GetRecentChangesAsync(1000, timeRange);
            var undoableChanges = await undoRedoOperationsService.GetUndoableChangesAsync();
            var redoableChanges = await undoRedoOperationsService.GetRedoableChangesAsync();

            var stats = new
            {
                total_changes = changes.Count,
                files_modified = changes.Select(c => c.FilePath).Distinct().Count(),
                operations = changes.GroupBy(c => c.Operation)
                    .ToDictionary(g => g.Key, g => g.Count()),
                undo_redo_stats = new
                {
                    undoable_changes = undoableChanges.Count,
                    redoable_changes = redoableChanges.Count,
                    undone_changes = changes.Count(c => c.IsUndone)
                },
                daily_activity = changes
                    .GroupBy(c => c.Timestamp.Date)
                    .OrderByDescending(g => g.Key)
                    .Take(7)
                    .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Count()),
                lines_changed = new
                {
                    added = changes.Sum(c => c.Details.LinesAdded),
                    removed = changes.Sum(c => c.Details.LinesRemoved),
                    modified = changes.Sum(c => c.Details.LinesModified)
                },
                recent_files = changes
                    .GroupBy(c => c.FilePath)
                    .OrderByDescending(g => g.Max(c => c.Timestamp))
                    .Take(10)
                    .Select(g => new
                    {
                        file_path = g.Key,
                        last_modified = g.Max(c => c.Timestamp),
                        change_count = g.Count()
                    })
                    .ToArray(),
                workspace_info = new
                {
                    workspace_path = CurrentWorkspacePath,
                    workspace_hash = appDataPathService.GetWorkspaceHash(CurrentWorkspacePath),
                    data_location = appDataPathService.GetWorkspaceDirectory(CurrentWorkspacePath)
                }
            };

            return stats;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get change stats: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get daily activity statistics
    /// </summary>
    public async Task<Dictionary<string, int>> GetDailyActivityAsync(int days = 7)
    {
        try
        {
            var timeRange = TimeSpan.FromDays(days);
            var changes = await GetRecentChangesAsync(1000, timeRange);

            return changes
                .GroupBy(c => c.Timestamp.Date)
                .OrderByDescending(g => g.Key)
                .Take(days)
                .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Count());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get daily activity: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get operation statistics (counts by operation type)
    /// </summary>
    public async Task<Dictionary<string, int>> GetOperationStatsAsync(TimeSpan? timeRange = null)
    {
        try
        {
            var changes = await GetRecentChangesAsync(1000, timeRange);

            return changes
                .GroupBy(c => c.Operation)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get operation stats: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get file modification statistics
    /// </summary>
    public async Task<object> GetFileModificationStatsAsync(TimeSpan? timeRange = null)
    {
        try
        {
            var changes = await GetRecentChangesAsync(1000, timeRange);

            var fileStats = changes
                .GroupBy(c => c.FilePath)
                .Select(g => new
                {
                    file_path = g.Key,
                    change_count = g.Count(),
                    last_modified = g.Max(c => c.Timestamp),
                    total_lines_added = g.Sum(c => c.Details.LinesAdded),
                    total_lines_removed = g.Sum(c => c.Details.LinesRemoved),
                    total_lines_modified = g.Sum(c => c.Details.LinesModified),
                    operations = g.GroupBy(c => c.Operation).ToDictionary(og => og.Key, og => og.Count())
                })
                .OrderByDescending(f => f.change_count)
                .Take(20)
                .ToArray();

            return new
            {
                total_files_modified = changes.Select(c => c.FilePath).Distinct().Count(),
                most_modified_files = fileStats,
                total_changes = changes.Count,
                total_lines_added = changes.Sum(c => c.Details.LinesAdded),
                total_lines_removed = changes.Sum(c => c.Details.LinesRemoved),
                total_lines_modified = changes.Sum(c => c.Details.LinesModified)
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get file modification stats: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Helper method to get recent changes with time filtering
    /// </summary>
    private async Task<List<ChangeRecord>> GetRecentChangesAsync(int maxRecords, TimeSpan? timeRange)
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
}
