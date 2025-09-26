using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;

namespace McpCodeEditor.Services;

/// <summary>
/// Service responsible for mass rename operations following Clean Code and SOLID principles.
/// Extracted from BatchOperationService to follow Single Responsibility Principle.
/// </summary>
public class MassRenameService(
    IBackupService backupService,
    IChangeTrackingService changeTracking,
    ILogger<MassRenameService> logger)
{
    /// <summary>
    /// Execute mass rename operations on files and directories
    /// </summary>
    public async Task<BatchOperationResult> ExecuteAsync(
        string rootPath,
        BatchRenameOptions options,
        IBatchProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new BatchOperationResult();
        string? backupId = null;

        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(options.SearchPattern))
            {
                return new BatchOperationResult
                {
                    Success = false,
                    Error = "Search pattern cannot be empty"
                };
            }

            if (!Directory.Exists(rootPath))
            {
                return new BatchOperationResult
                {
                    Success = false,
                    Error = $"Directory not found: {rootPath}"
                };
            }

            // Create backup if requested
            if (options.CreateBackup)
            {
                backupId = await backupService.CreateBackupAsync(rootPath, "mass_rename_operation");
                result.BackupId = backupId;
            }

            // Find items to rename
            List<string> items = await DiscoverItemsToRenameAsync(rootPath, options);
            result.TotalFiles = items.Count;

            logger.LogInformation($"Starting mass rename operation on {items.Count} items");

            // Prepare regex if needed
            Regex? searchRegex = PrepareSearchRegex(options);

            // Process items sequentially to avoid conflicts
            List<BatchFileResult> fileResults = await ProcessItemsSequentiallyAsync(
                items, options, searchRegex, backupId, progressReporter, cancellationToken);

            // Aggregate results
            AggregateResults(result, fileResults, stopwatch, options);

            logger.LogInformation($"Mass rename completed: {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mass rename operation failed");
            return new BatchOperationResult
            {
                Success = false,
                Error = $"Mass rename failed: {ex.Message}",
                Duration = stopwatch.Elapsed,
                BackupId = backupId
            };
        }
    }

    #region Private Helper Methods

    private static async Task<List<string>> DiscoverItemsToRenameAsync(string rootPath, BatchRenameOptions options)
    {
        var items = new List<string>();
        
        // Add files
        items.AddRange(Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories));

        // Add directories if requested (deepest first to avoid path conflicts)
        if (options.RenameDirectories)
        {
            items.AddRange(Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length));
        }

        return items;
    }

    private static Regex? PrepareSearchRegex(BatchRenameOptions options)
    {
        if (options.UseRegex)
        {
            return new Regex(options.SearchPattern, RegexOptions.Compiled);
        }
        return null;
    }

    private async Task<List<BatchFileResult>> ProcessItemsSequentiallyAsync(
        List<string> items,
        BatchRenameOptions options,
        Regex? searchRegex,
        string? backupId,
        IBatchProgressReporter? progressReporter,
        CancellationToken cancellationToken)
    {
        var fileResults = new List<BatchFileResult>();
        var processed = 0;

        foreach (string item in items)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            BatchFileResult fileResult = await ProcessSingleItemRenameAsync(item, options, searchRegex, cancellationToken);
            fileResults.Add(fileResult);

            processed++;
            progressReporter?.ReportProgress(processed, items.Count, item);
            progressReporter?.ReportFileCompleted(item, fileResult.Success, fileResult.Error);

            // Track changes if successful
            if (fileResult.Success && !string.IsNullOrEmpty(backupId))
            {
                await TrackRenameChangeAsync(item, fileResult, options, backupId);
            }
        }

        return fileResults;
    }

    private static async Task<BatchFileResult> ProcessSingleItemRenameAsync(
        string itemPath,
        BatchRenameOptions options,
        Regex? searchRegex,
        CancellationToken cancellationToken)
    {
        try
        {
            string itemName = Path.GetFileName(itemPath);
            string newName = GenerateNewName(itemName, options, searchRegex);

            // Only rename if name changed
            if (itemName != newName)
            {
                return await PerformRenameAsync(itemPath, itemName, newName);
            }
            else
            {
                return CreateSkippedResult(itemPath);
            }
        }
        catch (Exception ex)
        {
            return new BatchFileResult
            {
                FilePath = itemPath,
                Success = false,
                Operation = "rename",
                Error = ex.Message
            };
        }
    }

    private static string GenerateNewName(string itemName, BatchRenameOptions options, Regex? searchRegex)
    {
        if (options.UseRegex && searchRegex != null)
        {
            return searchRegex.Replace(itemName, options.ReplacePattern);
        }
        else
        {
            return itemName.Replace(options.SearchPattern, options.ReplacePattern);
        }
    }

    private static async Task<BatchFileResult> PerformRenameAsync(string itemPath, string itemName, string newName)
    {
        string directory = Path.GetDirectoryName(itemPath) ?? "";
        string newPath = Path.Combine(directory, newName);

        if (File.Exists(itemPath))
        {
            File.Move(itemPath, newPath);
        }
        else if (Directory.Exists(itemPath))
        {
            Directory.Move(itemPath, newPath);
        }

        return new BatchFileResult
        {
            FilePath = itemPath,
            Success = true,
            Operation = "rename",
            Details = new Dictionary<string, object>
            {
                ["originalName"] = itemName,
                ["newName"] = newName,
                ["newPath"] = newPath
            }
        };
    }

    private static BatchFileResult CreateSkippedResult(string itemPath)
    {
        return new BatchFileResult
        {
            FilePath = itemPath,
            Success = true,
            Operation = "rename",
            Details = new Dictionary<string, object>
            {
                ["skipped"] = "No rename needed"
            }
        };
    }

    private async Task TrackRenameChangeAsync(
        string originalPath,
        BatchFileResult fileResult,
        BatchRenameOptions options,
        string backupId)
    {
        await changeTracking.TrackChangeAsync(
            originalPath,
            originalPath,
            fileResult.Details.GetValueOrDefault("newPath", "").ToString() ?? "",
            $"Mass rename: '{options.SearchPattern}' -> '{options.ReplacePattern}'",
            backupId);
    }

    private static void AggregateResults(
        BatchOperationResult result,
        List<BatchFileResult> fileResults,
        System.Diagnostics.Stopwatch stopwatch,
        BatchRenameOptions options)
    {
        result.Files = fileResults;
        result.ProcessedFiles = fileResults.Count;
        result.SuccessfulFiles = fileResults.Count(f => f.Success);
        result.FailedFiles = fileResults.Count(f => !f.Success);
        result.Success = result.FailedFiles == 0;
        result.Duration = stopwatch.Elapsed;

        result.Message = result.Success
            ? $"Successfully renamed {result.SuccessfulFiles} items in {result.Duration.TotalSeconds:F2} seconds"
            : $"Renamed {result.SuccessfulFiles} of {result.ProcessedFiles} items with {result.FailedFiles} failures in {result.Duration.TotalSeconds:F2} seconds";

        result.Metadata["searchPattern"] = options.SearchPattern;
        result.Metadata["replacePattern"] = options.ReplacePattern;
        result.Metadata["useRegex"] = options.UseRegex;
        result.Metadata["renameDirectories"] = options.RenameDirectories;
    }

    #endregion
}
