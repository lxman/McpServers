using System.Text.Json;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for persisting and retrieving change records from the file system
/// </summary>
public class ChangeRecordPersistenceService(
    CodeEditorConfigurationService config,
    IAppDataPathService appDataPathService,
    IWorkspaceMetadataService? workspaceMetadataService = null) : IChangeRecordPersistenceService
{
    /// <summary>
    /// Gets the current workspace path from configuration
    /// </summary>
    private string CurrentWorkspacePath => config.DefaultWorkspace;

    /// <summary>
    /// Gets the workspace-specific changes directory (dynamically calculated)
    /// </summary>
    private string ChangeLogDirectory
    {
        get
        {
            var directory = appDataPathService.GetWorkspaceDirectory(CurrentWorkspacePath);
            appDataPathService.EnsureDirectoryExists(directory);
            return directory;
        }
    }

    /// <summary>
    /// Gets the workspace-specific changes file (dynamically calculated)
    /// </summary>
    private string ChangeLogFile
    {
        get
        {
            var file = appDataPathService.GetWorkspaceChangesFile(CurrentWorkspacePath);
            appDataPathService.EnsureDirectoryExists(Path.GetDirectoryName(file)!);
            return file;
        }
    }

    /// <summary>
    /// Load all change records from storage
    /// </summary>
    public async Task<List<ChangeRecord>> LoadChangeRecordsAsync()
    {
        var changes = new List<ChangeRecord>();

        if (!File.Exists(ChangeLogFile))
        {
            return changes;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(ChangeLogFile);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var change = JsonSerializer.Deserialize<ChangeRecord>(line);
                    if (change != null)
                    {
                        changes.Add(change);
                    }
                }
                catch
                {
                    // Skip invalid lines
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load change records: {ex.Message}", ex);
        }

        return changes;
    }

    /// <summary>
    /// Append a new change record to storage
    /// </summary>
    public async Task AppendChangeRecordAsync(ChangeRecord changeRecord)
    {
        // Update workspace metadata to track access
        if (workspaceMetadataService != null)
        {
            await workspaceMetadataService.UpdateLastAccessedAsync(CurrentWorkspacePath);
        }

        var jsonLine = JsonSerializer.Serialize(changeRecord) + Environment.NewLine;
        await File.AppendAllTextAsync(ChangeLogFile, jsonLine);
    }

    /// <summary>
    /// Rewrite all change records to storage (used for cleanup operations)
    /// </summary>
    public async Task RewriteChangeLogAsync(List<ChangeRecord> changes)
    {
        var lines = changes.Select(c => JsonSerializer.Serialize(c)).ToArray();
        await File.WriteAllLinesAsync(ChangeLogFile, lines);
    }

    /// <summary>
    /// Mark a change as undone
    /// </summary>
    public async Task MarkChangeAsUndoneAsync(string changeId, string undoChangeId)
    {
        try
        {
            var allChanges = await LoadChangeRecordsAsync();
            var changeRecord = allChanges.FirstOrDefault(c => c.Id == changeId);

            if (changeRecord != null)
            {
                changeRecord.IsUndone = true;
                changeRecord.UndoChangeId = undoChangeId;
                await RewriteChangeLogAsync(allChanges);
            }
        }
        catch
        {
            // Non-critical operation
        }
    }

    /// <summary>
    /// Mark a change as redone (undoes the undo)
    /// </summary>
    public async Task MarkChangeAsRedoneAsync(string changeId, string redoChangeId)
    {
        try
        {
            var allChanges = await LoadChangeRecordsAsync();
            var changeRecord = allChanges.FirstOrDefault(c => c.Id == changeId);

            if (changeRecord != null)
            {
                changeRecord.IsUndone = false;
                changeRecord.RedoChangeId = redoChangeId;
                await RewriteChangeLogAsync(allChanges);
            }
        }
        catch
        {
            // Non-critical operation
        }
    }

    /// <summary>
    /// Get detailed information about a specific change
    /// </summary>
    public async Task<ChangeRecord?> GetChangeDetailsAsync(string changeId)
    {
        try
        {
            var allChanges = await LoadChangeRecordsAsync();
            return allChanges.FirstOrDefault(c => c.Id == changeId);
        }
        catch
        {
            return null;
        }
    }
}
