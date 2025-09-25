using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;
using McpCodeEditor.Services.BatchOperations;

namespace McpCodeEditor.Services;

public class BatchOperationService(
    CodeEditorConfigurationService config,
    IBackupService backupService,
    IChangeTrackingService changeTracking,
    FileOperationsService fileService,
    ConversionService conversionService,
    BatchReplaceService batchReplaceService,
    BulkFormatService bulkFormatService,
    ILogger<BatchOperationService> logger)
{
    private readonly FileOperationsService _fileService = fileService;

    /// <summary>
    /// Perform batch replace operations across multiple files
    /// </summary>
    public async Task<BatchOperationResult> BatchReplaceAsync(
        string rootPath,
        BatchReplaceOptions options,
        IBatchProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to the specialized BatchReplaceService
        return await batchReplaceService.ExecuteAsync(rootPath, options, progressReporter, cancellationToken);
    }

    /// <summary>
    /// Perform bulk formatting operations on multiple files
    /// </summary>
    public async Task<BatchOperationResult> BulkFormatAsync(
        string rootPath,
        BatchFormatOptions options,
        IBatchProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to the specialized BulkFormatService
        return await bulkFormatService.ExecuteAsync(rootPath, options, progressReporter, cancellationToken);
    }

    /// <summary>
    /// Perform mass rename operations on files and directories
    /// </summary>
    public async Task<BatchOperationResult> MassRenameAsync(
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
            if (string.IsNullOrEmpty(options.SearchPattern))
            {
                return new BatchOperationResult
                {
                    Success = false,
                    Error = "Search pattern cannot be empty"
                };
            }

            // Validate and resolve path
            var resolvedPath = ValidateAndResolvePath(rootPath);
            if (resolvedPath == null)
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
                backupId = await backupService.CreateBackupAsync(resolvedPath, "mass_rename_operation");
                result.BackupId = backupId;
            }

            // Find items to rename (files and optionally directories)
            var items = new List<string>();
            items.AddRange(Directory.GetFiles(resolvedPath, "*", SearchOption.AllDirectories));

            if (options.RenameDirectories)
            {
                items.AddRange(Directory.GetDirectories(resolvedPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length)); // Process deepest directories first
            }

            result.TotalFiles = items.Count;

            logger.LogInformation($"Starting mass rename operation on {items.Count} items");

            // Prepare regex if needed
            Regex? searchRegex = null;
            if (options.UseRegex)
            {
                searchRegex = new Regex(options.SearchPattern, RegexOptions.Compiled);
            }

            // Process items sequentially (to avoid conflicts with directory structure changes)
            var fileResults = new List<BatchFileResult>();
            var processed = 0;

            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var fileResult = await ProcessItemRenameAsync(item, options, searchRegex, cancellationToken);
                fileResults.Add(fileResult);

                processed++;
                progressReporter?.ReportProgress(processed, items.Count, item);
                progressReporter?.ReportFileCompleted(item, fileResult.Success, fileResult.Error);

                // Track changes if successful
                if (fileResult.Success && !string.IsNullOrEmpty(backupId))
                {
                    await changeTracking.TrackChangeAsync(
                        item,
                        item,
                        fileResult.Details.GetValueOrDefault("newPath", "").ToString() ?? "",
                        $"Mass rename: '{options.SearchPattern}' -> '{options.ReplacePattern}'",
                        backupId);
                }
            }

            // Aggregate results
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

    /// <summary>
    /// Perform bulk file format conversion operations
    /// </summary>
    public async Task<BatchOperationResult> BulkConvertAsync(
        string rootPath,
        BatchConvertOptions options,
        IBatchProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new BatchOperationResult();
        string? backupId = null;

        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(options.FromExtension) || string.IsNullOrEmpty(options.ToExtension))
            {
                return new BatchOperationResult
                {
                    Success = false,
                    Error = "Both FromExtension and ToExtension must be specified"
                };
            }

            // Validate and resolve path
            var resolvedPath = ValidateAndResolvePath(rootPath);
            if (resolvedPath == null)
            {
                return new BatchOperationResult
                {
                    Success = false,
                    Error = $"Directory not found: {rootPath}"
                };
            }

            // Ensure extensions start with dot
            if (!options.FromExtension.StartsWith("."))
                options.FromExtension = "." + options.FromExtension;
            if (!options.ToExtension.StartsWith("."))
                options.ToExtension = "." + options.ToExtension;

            // Check if conversion is supported using ConversionService
            if (!ConversionService.IsConversionSupported(options.FromExtension, options.ToExtension))
            {
                return new BatchOperationResult
                {
                    Success = false,
                    Error = $"Conversion from '{options.FromExtension}' to '{options.ToExtension}' is not currently supported"
                };
            }

            // Create backup if requested
            if (options.CreateBackup)
            {
                backupId = await backupService.CreateBackupAsync(resolvedPath, "bulk_convert_operation");
                result.BackupId = backupId;
            }

            // Find files to convert
            var files = Directory.GetFiles(resolvedPath, $"*{options.FromExtension}", SearchOption.AllDirectories)
                .Where(f => config.AllowedExtensions.Contains(options.FromExtension.ToLowerInvariant()))
                .ToList();

            result.TotalFiles = files.Count;

            logger.LogInformation($"Starting bulk convert operation on {files.Count} files from '{options.FromExtension}' to '{options.ToExtension}'");

            // Process files in parallel
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
                    var fileResult = await ProcessFileConvertAsync(file, options, ct);
                    fileResults.Add(fileResult);

                    var currentProcessed = Interlocked.Increment(ref processed);
                    progressReporter?.ReportProgress(currentProcessed, files.Count, file);
                    progressReporter?.ReportFileCompleted(file, fileResult.Success, fileResult.Error);

                    // Track changes if successful
                    if (fileResult.Success && !string.IsNullOrEmpty(backupId))
                    {
                        await changeTracking.TrackChangeAsync(
                            file,
                            fileResult.Details.GetValueOrDefault("originalContent", "").ToString() ?? "",
                            fileResult.Details.GetValueOrDefault("convertedContent", "").ToString() ?? "",
                            $"Bulk convert: '{options.FromExtension}' -> '{options.ToExtension}'",
                            backupId);
                    }
                });

            // Aggregate results
            result.Files = fileResults.ToList();
            result.ProcessedFiles = result.Files.Count;
            result.SuccessfulFiles = result.Files.Count(f => f.Success);
            result.FailedFiles = result.Files.Count(f => !f.Success);
            result.Success = result.FailedFiles == 0;
            result.Duration = stopwatch.Elapsed;

            result.Message = result.Success
                ? $"Successfully converted {result.SuccessfulFiles} files from '{options.FromExtension}' to '{options.ToExtension}' in {result.Duration.TotalSeconds:F2} seconds"
                : $"Converted {result.SuccessfulFiles} of {result.ProcessedFiles} files with {result.FailedFiles} failures in {result.Duration.TotalSeconds:F2} seconds";

            result.Metadata["fromExtension"] = options.FromExtension;
            result.Metadata["toExtension"] = options.ToExtension;
            result.Metadata["deleteOriginal"] = options.DeleteOriginal;
            result.Metadata["supportedConversions"] = ConversionService.GetSupportedConversions();

            logger.LogInformation($"Bulk convert completed: {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bulk convert operation failed");
            return new BatchOperationResult
            {
                Success = false,
                Error = $"Bulk convert failed: {ex.Message}",
                Duration = stopwatch.Elapsed,
                BackupId = backupId
            };
        }
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

    #region Helper Methods

    private static async Task<BatchFileResult> ProcessItemRenameAsync(
        string itemPath,
        BatchRenameOptions options,
        Regex? searchRegex,
        CancellationToken cancellationToken)
    {
        try
        {
            var itemName = Path.GetFileName(itemPath);
            string newName;

            if (options.UseRegex && searchRegex != null)
            {
                newName = searchRegex.Replace(itemName, options.ReplacePattern);
            }
            else
            {
                newName = itemName.Replace(options.SearchPattern, options.ReplacePattern);
            }

            // Only rename if name changed
            if (itemName != newName)
            {
                var directory = Path.GetDirectoryName(itemPath) ?? "";
                var newPath = Path.Combine(directory, newName);

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
            else
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

    private async Task<BatchFileResult> ProcessFileConvertAsync(
        string filePath,
        BatchConvertOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var originalContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            string convertedContent;

            // Perform the conversion using ConversionService
            convertedContent = await ConversionService.ConvertContentAsync(originalContent, options.FromExtension, options.ToExtension, options.ConversionSettings);

            // Generate new file path
            var directory = Path.GetDirectoryName(filePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var newFilePath = Path.Combine(directory, fileName + options.ToExtension);

            // Write converted content to new file
            await File.WriteAllTextAsync(newFilePath, convertedContent, cancellationToken);

            // Delete original file if requested
            if (options.DeleteOriginal)
            {
                File.Delete(filePath);
            }

            return new BatchFileResult
            {
                FilePath = filePath,
                Success = true,
                Operation = "convert",
                Details = new Dictionary<string, object>
                {
                    ["originalContent"] = originalContent,
                    ["convertedContent"] = convertedContent,
                    ["originalPath"] = filePath,
                    ["newPath"] = newFilePath,
                    ["fromExtension"] = options.FromExtension,
                    ["toExtension"] = options.ToExtension,
                    ["originalDeleted"] = options.DeleteOriginal,
                    ["sizeChange"] = convertedContent.Length - originalContent.Length
                }
            };
        }
        catch (Exception ex)
        {
            return new BatchFileResult
            {
                FilePath = filePath,
                Success = false,
                Operation = "convert",
                Error = ex.Message
            };
        }
    }

    #endregion
}
