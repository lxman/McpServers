using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;

namespace McpCodeEditor.Services.BatchOperations;

/// <summary>
/// Service responsible for batch text replacement operations across multiple files.
/// Follows Single Responsibility Principle by handling only batch replace functionality.
/// </summary>
public class BatchReplaceService(
    CodeEditorConfigurationService config,
    IBackupService backupService,
    IChangeTrackingService changeTracking,
    FileOperationsService fileService,
    ILogger<BatchReplaceService> logger)
{
    private readonly FileOperationsService _fileService = fileService;

    /// <summary>
    /// Perform batch replace operations across multiple files
    /// </summary>
    public async Task<BatchOperationResult> ExecuteAsync(
        string rootPath,
        BatchReplaceOptions options,
        IBatchProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new BatchOperationResult();
        string? backupId = null;

        try
        {
            // Validate inputs and resolve path
            var pathValidationResult = ValidateInputs(rootPath, options);
            if (!pathValidationResult.IsValid)
            {
                return new BatchOperationResult
                {
                    Success = false,
                    Error = pathValidationResult.ErrorMessage
                };
            }

            var resolvedPath = pathValidationResult.ResolvedPath!;

            // Create backup if requested
            if (options.CreateBackup)
            {
                backupId = await backupService.CreateBackupAsync(resolvedPath, "batch_replace_operation");
                result.BackupId = backupId;
            }

            // Find files to process
            var files = FindFiles(resolvedPath, options.FilePattern, options.ExcludeDirectories);
            result.TotalFiles = files.Count;

            logger.LogInformation($"Starting batch replace operation on {files.Count} files");

            // Prepare regex if needed
            var searchRegex = PrepareSearchRegex(options);

            // Process files in parallel
            var fileResults = await ProcessFilesAsync(files, options, searchRegex, progressReporter, backupId, cancellationToken);

            // Aggregate results
            PopulateResult(result, fileResults, stopwatch.Elapsed, options);

            logger.LogInformation($"Batch replace completed: {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch replace operation failed");
            return new BatchOperationResult
            {
                Success = false,
                Error = $"Batch replace failed: {ex.Message}",
                Duration = stopwatch.Elapsed,
                BackupId = backupId
            };
        }
    }

    private PathValidationResult ValidateInputs(string rootPath, BatchReplaceOptions options)
    {
        if (string.IsNullOrEmpty(options.SearchPattern))
        {
            return PathValidationResult.Invalid("Search pattern cannot be empty");
        }

        var resolvedPath = ValidateAndResolvePath(rootPath);
        if (resolvedPath == null)
        {
            return PathValidationResult.Invalid($"Directory not found: {rootPath}");
        }

        return PathValidationResult.Valid(resolvedPath);
    }

    /// <summary>
    /// Validates and resolves a file path (relative or absolute) to an absolute path
    /// </summary>
    private string? ValidateAndResolvePath(string path)
    {
        try
        {
            // If it's already an absolute path, validate it directly
            if (Path.IsPathRooted(path))
            {
                return Directory.Exists(path) ? Path.GetFullPath(path) : null;
            }

            // For relative paths, resolve relative to the current workspace
            var basePath = config.DefaultWorkspace;
            var fullPath = Path.Combine(basePath, path);
            var resolvedPath = Path.GetFullPath(fullPath);

            return Directory.Exists(resolvedPath) ? resolvedPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static Regex? PrepareSearchRegex(BatchReplaceOptions options)
    {
        if (!options.UseRegex) return null;

        var regexOptions = RegexOptions.Compiled;
        if (!options.CaseSensitive)
            regexOptions |= RegexOptions.IgnoreCase;

        return new Regex(options.SearchPattern, regexOptions);
    }

    private async Task<List<BatchFileResult>> ProcessFilesAsync(
        List<string> files,
        BatchReplaceOptions options,
        Regex? searchRegex,
        IBatchProgressReporter? progressReporter,
        string? backupId,
        CancellationToken cancellationToken)
    {
        var fileResults = new ConcurrentBag<BatchFileResult>();
        var processed = 0;

        await Parallel.ForEachAsync(files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            },
            async (file, ct) =>
            {
                var fileResult = await ProcessSingleFileAsync(file, options, searchRegex, ct);
                fileResults.Add(fileResult);

                var currentProcessed = Interlocked.Increment(ref processed);
                progressReporter?.ReportProgress(currentProcessed, files.Count, file);
                progressReporter?.ReportFileCompleted(file, fileResult.Success, fileResult.Error);

                // Track changes if successful
                if (fileResult.Success && !string.IsNullOrEmpty(backupId))
                {
                    await TrackFileChangeAsync(file, fileResult, options, backupId);
                }
            });

        return fileResults.ToList();
    }

    private static async Task<BatchFileResult> ProcessSingleFileAsync(
        string filePath,
        BatchReplaceOptions options,
        Regex? searchRegex,
        CancellationToken cancellationToken)
    {
        try
        {
            var originalContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var modifiedContent = ApplyReplacements(originalContent, options, searchRegex);

            if (originalContent != modifiedContent)
            {
                await File.WriteAllTextAsync(filePath, modifiedContent, cancellationToken);
                return CreateSuccessResult(filePath, originalContent, modifiedContent, options, searchRegex);
            }
            else
            {
                return CreateSkippedResult(filePath, originalContent);
            }
        }
        catch (Exception ex)
        {
            return new BatchFileResult
            {
                FilePath = filePath,
                Success = false,
                Operation = "replace",
                Error = ex.Message
            };
        }
    }

    private static string ApplyReplacements(string content, BatchReplaceOptions options, Regex? searchRegex)
    {
        if (options.UseRegex && searchRegex != null)
        {
            return searchRegex.Replace(content, options.ReplaceWith);
        }
        else
        {
            var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return content.Replace(options.SearchPattern, options.ReplaceWith, comparison);
        }
    }

    private static BatchFileResult CreateSuccessResult(
        string filePath, 
        string originalContent, 
        string modifiedContent, 
        BatchReplaceOptions options, 
        Regex? searchRegex)
    {
        return new BatchFileResult
        {
            FilePath = filePath,
            Success = true,
            Operation = "replace",
            Details = new Dictionary<string, object>
            {
                ["originalSize"] = originalContent.Length,
                ["modifiedSize"] = modifiedContent.Length,
                ["changesCount"] = CountOccurrences(originalContent, options.SearchPattern, options.UseRegex, searchRegex, options.CaseSensitive)
            }
        };
    }

    private static BatchFileResult CreateSkippedResult(string filePath, string originalContent)
    {
        return new BatchFileResult
        {
            FilePath = filePath,
            Success = true,
            Operation = "replace",
            Details = new Dictionary<string, object>
            {
                ["originalSize"] = originalContent.Length,
                ["modifiedSize"] = originalContent.Length,
                ["changesCount"] = 0,
                ["skipped"] = "No changes needed"
            }
        };
    }

    private async Task TrackFileChangeAsync(string file, BatchFileResult fileResult, BatchReplaceOptions options, string backupId)
    {
        await changeTracking.TrackChangeAsync(
            file,
            fileResult.Details.GetValueOrDefault("originalSize", 0).ToString() ?? "0",
            fileResult.Details.GetValueOrDefault("modifiedSize", 0).ToString() ?? "0",
            $"Batch replace: '{options.SearchPattern}' -> '{options.ReplaceWith}'",
            backupId);
    }

    private List<string> FindFiles(string rootPath, string filePattern, List<string> excludeDirectories)
    {
        var files = new List<string>();
        var searchPattern = string.IsNullOrEmpty(filePattern) ? "*" : filePattern;

        try
        {
            var allFiles = Directory.GetFiles(rootPath, searchPattern, SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                if (ShouldIncludeFile(file, excludeDirectories))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, $"Error finding files in {rootPath}");
        }

        return files;
    }

    private bool ShouldIncludeFile(string file, List<string> excludeDirectories)
    {
        // Check if file is in excluded directory
        var directory = Path.GetDirectoryName(file);
        if (directory != null && excludeDirectories.Any(excluded =>
            directory.Contains(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Check if file extension is allowed
        var extension = Path.GetExtension(file).ToLowerInvariant();
        return config.AllowedExtensions.Contains(extension);
    }

    private static void PopulateResult(BatchOperationResult result, List<BatchFileResult> fileResults, TimeSpan duration, BatchReplaceOptions options)
    {
        result.Files = fileResults;
        result.ProcessedFiles = fileResults.Count;
        result.SuccessfulFiles = fileResults.Count(f => f.Success);
        result.FailedFiles = fileResults.Count(f => !f.Success);
        result.Success = result.FailedFiles == 0;
        result.Duration = duration;

        result.Message = result.Success
            ? $"Successfully processed {result.SuccessfulFiles} files in {result.Duration.TotalSeconds:F2} seconds"
            : $"Processed {result.ProcessedFiles} files with {result.FailedFiles} failures in {result.Duration.TotalSeconds:F2} seconds";

        result.Metadata["searchPattern"] = options.SearchPattern;
        result.Metadata["replaceWith"] = options.ReplaceWith;
        result.Metadata["useRegex"] = options.UseRegex;
        result.Metadata["caseSensitive"] = options.CaseSensitive;
    }

    private static int CountOccurrences(string text, string pattern, bool useRegex, Regex? regex, bool caseSensitive)
    {
        try
        {
            if (useRegex && regex != null)
            {
                return regex.Matches(text).Count;
            }
            else
            {
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var count = 0;
                var index = 0;
                while ((index = text.IndexOf(pattern, index, comparison)) != -1)
                {
                    count++;
                    index += pattern.Length;
                }
                return count;
            }
        }
        catch
        {
            return 0;
        }
    }

    private record PathValidationResult(bool IsValid, string? ResolvedPath = null, string? ErrorMessage = null)
    {
        public static PathValidationResult Valid(string resolvedPath) => new(true, resolvedPath);
        public static PathValidationResult Invalid(string errorMessage) => new(false, null, errorMessage);
    }
}
