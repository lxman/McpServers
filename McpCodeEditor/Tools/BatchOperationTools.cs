using System.ComponentModel;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Options;
using McpCodeEditor.Services;
using McpCodeEditor.Tools.Common;
using ModelContextProtocol.Server;

namespace McpCodeEditor.Tools;

/// <summary>
/// MCP tools for batch and bulk operations across multiple files.
/// Focused class for batch operations following Single Responsibility Principle.
/// </summary>
[McpServerToolType]
public class BatchOperationTools(BatchOperationService batchOperationService) : BaseToolClass
{
    #region Batch Operations

    [McpServerTool]
    [Description("Perform batch text replacement operations across multiple files")]
    public async Task<string> BatchReplaceAsync(
        [Description("Root directory to search")]
        string rootPath,
        [Description("Text or regex pattern to search for")]
        string searchPattern,
        [Description("Replacement text")]
        string replaceWith,
        [Description("File pattern to include (e.g., '*.cs', '*.txt')")]
        string filePattern = "*",
        [Description("Use regular expressions")]
        bool useRegex = false,
        [Description("Case sensitive search")]
        bool caseSensitive = false,
        [Description("Directories to exclude (comma-separated)")]
        string excludeDirectories = "bin,obj,node_modules,.git",
        [Description("Create backup before operations")]
        bool createBackup = true)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(rootPath, nameof(rootPath));
            ValidateRequiredParameter(searchPattern, nameof(searchPattern));
            ValidateRequiredParameter(replaceWith, nameof(replaceWith));

            var options = new BatchReplaceOptions
            {
                SearchPattern = searchPattern,
                ReplaceWith = replaceWith,
                FilePattern = filePattern,
                UseRegex = useRegex,
                CaseSensitive = caseSensitive,
                ExcludeDirectories = excludeDirectories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                CreateBackup = createBackup
            };

            BatchOperationResult result = await batchOperationService.BatchReplaceAsync(rootPath, options);
            return result;
        });
    }

    [McpServerTool]
    [Description("Perform bulk code formatting operations on multiple files")]
    public async Task<string> BulkFormatAsync(
        [Description("Root directory to format")]
        string rootPath,
        [Description("File pattern to include (e.g., '*.cs', '*.js')")]
        string filePattern = "*.cs",
        [Description("Directories to exclude (comma-separated)")]
        string excludeDirectories = "bin,obj,node_modules,.git",
        [Description("Code formatting style (default, custom)")]
        string codeStyle = "default",
        [Description("Create backup before operations")]
        bool createBackup = true)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(rootPath, nameof(rootPath));

            var options = new BatchFormatOptions
            {
                FilePattern = filePattern,
                ExcludeDirectories = excludeDirectories.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                CodeStyle = codeStyle,
                CreateBackup = createBackup
            };

            BatchOperationResult result = await batchOperationService.BulkFormatAsync(rootPath, options);
            return result;
        });
    }

    [McpServerTool]
    [Description("Perform mass rename operations on files and directories")]
    public async Task<string> MassRenameAsync(
        [Description("Root directory to search")]
        string rootPath,
        [Description("Pattern to search for in file/directory names")]
        string searchPattern,
        [Description("Replacement pattern for names")]
        string replacePattern,
        [Description("Use regular expressions")]
        bool useRegex = false,
        [Description("Also rename directories")]
        bool renameDirectories = false,
        [Description("Create backup before operations")]
        bool createBackup = true)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(rootPath, nameof(rootPath));
            ValidateRequiredParameter(searchPattern, nameof(searchPattern));
            ValidateRequiredParameter(replacePattern, nameof(replacePattern));

            var options = new BatchRenameOptions
            {
                SearchPattern = searchPattern,
                ReplacePattern = replacePattern,
                UseRegex = useRegex,
                RenameDirectories = renameDirectories,
                CreateBackup = createBackup
            };

            BatchOperationResult result = await batchOperationService.MassRenameAsync(rootPath, options);
            return result;
        });
    }

    [McpServerTool]
    [Description("Bulk convert files from one format to another")]
    public async Task<string> BulkConvertAsync(
        [Description("Root directory to search")]
        string rootPath,
        [Description("Source file extension (e.g., '.txt')")]
        string fromExtension,
        [Description("Target file extension (e.g., '.md')")]
        string toExtension,
        [Description("Delete original files after conversion")]
        bool deleteOriginal = false,
        [Description("Create backup before operations")]
        bool createBackup = true)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(rootPath, nameof(rootPath));
            ValidateRequiredParameter(fromExtension, nameof(fromExtension));
            ValidateRequiredParameter(toExtension, nameof(toExtension));

            var options = new BatchConvertOptions
            {
                FromExtension = fromExtension,
                ToExtension = toExtension,
                DeleteOriginal = deleteOriginal,
                CreateBackup = createBackup
            };

            BatchOperationResult result = await batchOperationService.BulkConvertAsync(rootPath, options);
            return result;
        });
    }

    #endregion
}
