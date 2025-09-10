using System.Collections.Concurrent;
using McpCodeEditor.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;

namespace McpCodeEditor.Services.BatchOperations;

/// <summary>
/// Service responsible for bulk code formatting operations across multiple files.
/// Follows Single Responsibility Principle by handling only bulk formatting functionality.
/// </summary>
public class BulkFormatService(
    CodeEditorConfigurationService config,
    IBackupService backupService,
    IChangeTrackingService changeTracking,
    FileOperationsService fileService,
    ILogger<BulkFormatService> logger)
{
    private readonly FileOperationsService _fileService = fileService;

    /// <summary>
    /// Perform bulk formatting operations on multiple files
    /// </summary>
    public async Task<BatchOperationResult> ExecuteAsync(
        string rootPath,
        BatchFormatOptions options,
        IBatchProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new BatchOperationResult();
        string? backupId = null;

        try
        {
            // Validate and resolve path
            string? resolvedPath = ValidateAndResolvePath(rootPath);
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
                backupId = await backupService.CreateBackupAsync(resolvedPath, "bulk_format_operation");
                result.BackupId = backupId;
            }

            // Find files to format
            List<string> files = FindFiles(resolvedPath, options.FilePattern, options.ExcludeDirectories);
            result.TotalFiles = files.Count;

            logger.LogInformation($"Starting bulk format operation on {files.Count} files");

            // Process files in parallel
            List<BatchFileResult> fileResults = await ProcessFilesAsync(files, progressReporter, backupId, cancellationToken);

            // Aggregate results
            PopulateResult(result, fileResults, stopwatch.Elapsed, options);

            logger.LogInformation($"Bulk format completed: {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bulk format operation failed");
            return new BatchOperationResult
            {
                Success = false,
                Error = $"Bulk format failed: {ex.Message}",
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
            string basePath = config.DefaultWorkspace;
            string fullPath = Path.Combine(basePath, path);
            string resolvedPath = Path.GetFullPath(fullPath);

            return Directory.Exists(resolvedPath) ? resolvedPath : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<BatchFileResult>> ProcessFilesAsync(
        List<string> files,
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
                BatchFileResult fileResult = await ProcessFileFormatAsync(file, ct);
                fileResults.Add(fileResult);

                int currentProcessed = Interlocked.Increment(ref processed);
                progressReporter?.ReportProgress(currentProcessed, files.Count, file);
                progressReporter?.ReportFileCompleted(file, fileResult.Success, fileResult.Error);

                // Track changes if successful
                if (fileResult.Success && !string.IsNullOrEmpty(backupId))
                {
                    await TrackFileChangeAsync(file, fileResult, backupId);
                }
            });

        return fileResults.ToList();
    }

    private static async Task<BatchFileResult> ProcessFileFormatAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            string originalContent = await File.ReadAllTextAsync(filePath, cancellationToken);

            // Only format C# files for now
            if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return new BatchFileResult
                {
                    FilePath = filePath,
                    Success = true,
                    Operation = "format",
                    Details = new Dictionary<string, object>
                    {
                        ["skipped"] = "Unsupported file type for formatting"
                    }
                };
            }

            // Parse and format using Roslyn
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(originalContent, cancellationToken: cancellationToken);
            SyntaxNode root = await syntaxTree.GetRootAsync(cancellationToken);

            using var workspace = new AdhocWorkspace();
            SyntaxNode formattedRoot = Formatter.Format(root, workspace, cancellationToken: cancellationToken);
            string formattedContent = formattedRoot.ToFullString();

            // Only write if content changed
            if (originalContent != formattedContent)
            {
                await File.WriteAllTextAsync(filePath, formattedContent, cancellationToken);

                return new BatchFileResult
                {
                    FilePath = filePath,
                    Success = true,
                    Operation = "format",
                    Details = new Dictionary<string, object>
                    {
                        ["originalContent"] = originalContent,
                        ["formattedContent"] = formattedContent,
                        ["sizeChange"] = formattedContent.Length - originalContent.Length
                    }
                };
            }
            else
            {
                return new BatchFileResult
                {
                    FilePath = filePath,
                    Success = true,
                    Operation = "format",
                    Details = new Dictionary<string, object>
                    {
                        ["skipped"] = "Already properly formatted"
                    }
                };
            }
        }
        catch (Exception ex)
        {
            return new BatchFileResult
            {
                FilePath = filePath,
                Success = false,
                Operation = "format",
                Error = ex.Message
            };
        }
    }

    private async Task TrackFileChangeAsync(string file, BatchFileResult fileResult, string backupId)
    {
        await changeTracking.TrackChangeAsync(
            file,
            fileResult.Details.GetValueOrDefault("originalContent", "").ToString() ?? "",
            fileResult.Details.GetValueOrDefault("formattedContent", "").ToString() ?? "",
            "Bulk format operation",
            backupId);
    }

    private List<string> FindFiles(string rootPath, string filePattern, List<string> excludeDirectories)
    {
        var files = new List<string>();
        string searchPattern = string.IsNullOrEmpty(filePattern) ? "*" : filePattern;

        try
        {
            string[] allFiles = Directory.GetFiles(rootPath, searchPattern, SearchOption.AllDirectories);

            foreach (string file in allFiles)
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
        string? directory = Path.GetDirectoryName(file);
        if (directory != null && excludeDirectories.Any(excluded =>
            directory.Contains(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Check if file extension is allowed
        string extension = Path.GetExtension(file).ToLowerInvariant();
        return config.AllowedExtensions.Contains(extension);
    }

    private static void PopulateResult(BatchOperationResult result, List<BatchFileResult> fileResults, TimeSpan duration, BatchFormatOptions options)
    {
        result.Files = fileResults;
        result.ProcessedFiles = fileResults.Count;
        result.SuccessfulFiles = fileResults.Count(f => f.Success);
        result.FailedFiles = fileResults.Count(f => !f.Success);
        result.Success = result.FailedFiles == 0;
        result.Duration = duration;

        result.Message = result.Success
            ? $"Successfully formatted {result.SuccessfulFiles} files in {result.Duration.TotalSeconds:F2} seconds"
            : $"Formatted {result.SuccessfulFiles} of {result.ProcessedFiles} files with {result.FailedFiles} failures in {result.Duration.TotalSeconds:F2} seconds";

        result.Metadata["filePattern"] = options.FilePattern;
        result.Metadata["formattingStyle"] = options.CodeStyle ?? "default";
    }
}
