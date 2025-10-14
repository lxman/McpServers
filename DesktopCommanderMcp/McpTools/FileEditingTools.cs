using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopCommanderMcp.Common;
using DesktopCommanderMcp.Services;
using DesktopCommanderMcp.Services.AdvancedFileEditing;
using DesktopCommanderMcp.Services.AdvancedFileEditing.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for advanced file editing operations with approval workflow
/// </summary>
[McpServerToolType]
public class FileEditingTools(
    FileEditor fileEditor,
    EditApprovalService approvalService,
    FileVersionService versionService,
    ILogger<FileEditingTools> logger)
{
    [McpServerTool, DisplayName("prepare_replace_lines")]
    [Description("PHASE 1: Prepare to replace a range of lines in a file. Returns an approval token for review.")]
    public async Task<string> PrepareReplaceLines(
        [Description("Full path to the file")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (inclusive)")] int endLine,
        [Description("New content to replace the lines with")] string newContent,
        [Description("Version token from previous read operation")] string versionToken,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            EditResult result = await fileEditor.PrepareReplaceFileLines(
                filePath,
                startLine,
                endLine,
                newContent,
                versionToken,
                createBackup);

            return JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing line replacement in: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("prepare_insert_after_line")]
    [Description("PHASE 1: Prepare to insert content after a specific line. Returns an approval token for review.")]
    public async Task<string> PrepareInsertAfterLine(
        [Description("Full path to the file")] string filePath,
        [Description("Line number to insert after")] int afterLine,
        [Description("Content to insert")] string content,
        [Description("Version token from previous read operation")] string versionToken,
        [Description("Maintain indentation from target line (default: true)")] bool maintainIndentation = true,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            EditResult result = await fileEditor.PrepareInsertAfterLine(
                filePath,
                afterLine,
                content,
                versionToken,
                maintainIndentation,
                createBackup);

            return JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing insert in: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("prepare_delete_lines")]
    [Description("PHASE 1: Prepare to delete a range of lines. Returns an approval token for review.")]
    public async Task<string> PrepareDeleteLines(
        [Description("Full path to the file")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (inclusive)")] int endLine,
        [Description("Version token from previous read operation")] string versionToken,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            EditResult result = await fileEditor.PrepareDeleteLines(
                filePath,
                startLine,
                endLine,
                versionToken,
                createBackup);

            return JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing line deletion in: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("prepare_replace_in_file")]
    [Description("PHASE 1: Prepare to replace text patterns in a file. Returns an approval token for review.")]
    public async Task<string> PrepareReplaceInFile(
        [Description("Full path to the file")] string filePath,
        [Description("Text pattern to search for")] string searchPattern,
        [Description("Replacement text")] string replaceWith,
        [Description("Version token from previous read operation")] string versionToken,
        [Description("Case sensitive search (default: false)")] bool caseSensitive = false,
        [Description("Use regular expressions (default: false)")] bool useRegex = false,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            EditResult result = await fileEditor.PrepareReplaceInFile(
                filePath,
                searchPattern,
                replaceWith,
                versionToken,
                caseSensitive,
                useRegex,
                createBackup);

            return JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preparing text replacement in: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("approve_edit")]
    [Description("PHASE 2: Approve and apply a pending edit. Requires confirmation string 'APPROVE'.")]
    public async Task<string> ApproveEdit(
        [Description("Approval token from the prepare operation")] string approvalToken,
        [Description("Must be exactly 'APPROVE' to confirm")] string confirmation)
    {
        try
        {
            if (confirmation != "APPROVE")
            {
                return JsonSerializer.Serialize(
                    new { success = false, error = "Confirmation must be exactly 'APPROVE'" }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            // Get the pending edit to check if it exists
            IReadOnlyList<PendingEdit> pendingEdits = approvalService.GetAllPendingEdits();
            PendingEdit? pendingEdit = pendingEdits.FirstOrDefault(pe => pe.ApprovalToken == approvalToken);
            
            if (pendingEdit == null)
            {
                logger.LogWarning("Invalid or expired approval token: {Token}", approvalToken);
                return JsonSerializer.Serialize(
                    new { success = false, error = "Invalid or expired approval token. Approval tokens expire after 5 minutes." }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            string fullPath = Path.GetFullPath(pendingEdit.FilePath);
            
            // Get the current version token to validate the file hasn't changed
            string currentVersionToken = FileVersionService.ComputeVersionToken(fullPath);
            
            EditResult result = await fileEditor.ApplyPendingEdit(approvalToken, currentVersionToken);
            
            if (!result.Success)
            {
                logger.LogError("Failed to apply edit for file {Path}: {Error}", fullPath, result.ErrorDetails);
                return JsonSerializer.Serialize(
                    new { success = false, error = result.ErrorDetails }, 
                    SerializerOptions.JsonOptionsIndented);
            }
            
            logger.LogInformation("Successfully applied edit to file: {Path}", fullPath);
            return JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error approving edit with token: {Token}", approvalToken);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("cancel_edit")]
    [Description("Cancel a pending edit that hasn't been approved yet")]
    public Task<string> CancelEdit(
        [Description("Approval token from the prepare operation")] string approvalToken)
    {
        try
        {
            bool result = approvalService.CancelPendingEdit(approvalToken);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = result,
                approvalToken,
                message = result ? "Edit cancelled successfully" : "Edit not found or already cancelled"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling edit with token: {Token}", approvalToken);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("list_pending_edits")]
    [Description("List all pending edits awaiting approval")]
    public Task<string> ListPendingEdits()
    {
        try
        {
            IReadOnlyList<PendingEdit> result = approvalService.GetAllPendingEdits();
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                pendingCount = result.Count,
                pendingEdits = result
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing pending edits");
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("find_in_file")]
    [Description("Find lines in a file matching a pattern")]
    public async Task<string> FindInFile(
        [Description("Full path to the file")] string filePath,
        [Description("Text pattern to search for")] string pattern,
        [Description("Case sensitive search (default: false)")] bool caseSensitive = false,
        [Description("Use regular expressions (default: false)")] bool useRegex = false)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found" }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            string[] allLines = await File.ReadAllLinesAsync(filePath);
            var matches = new List<object>();

            for (var i = 0; i < allLines.Length; i++)
            {
                var isMatch = false;
                
                if (useRegex)
                {
                    var regex = new Regex(
                        pattern,
                        caseSensitive 
                            ? RegexOptions.None 
                            : RegexOptions.IgnoreCase);
                    isMatch = regex.IsMatch(allLines[i]);
                }
                else
                {
                    StringComparison comparison = caseSensitive 
                        ? StringComparison.Ordinal 
                        : StringComparison.OrdinalIgnoreCase;
                    isMatch = allLines[i].Contains(pattern, comparison);
                }

                if (isMatch)
                {
                    matches.Add(new
                    {
                        lineNumber = i + 1,
                        content = allLines[i]
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                pattern,
                matchesFound = matches.Count,
                matches
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching in file: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("analyze_indentation")]
    [Description("Analyze indentation patterns in a file to determine if spaces or tabs are used")]
    public async Task<string> AnalyzeIndentation(
        [Description("Full path to the file")] string filePath)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found" }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            string[] lines = await File.ReadAllLinesAsync(filePath);
            var spacesCount = 0;
            var tabsCount = 0;
            var mixedCount = 0;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                char[] leadingWhitespace = line.TakeWhile(char.IsWhiteSpace).ToArray();
                if (leadingWhitespace.Length == 0) continue;

                bool hasSpaces = leadingWhitespace.Contains(' ');
                bool hasTabs = leadingWhitespace.Contains('\t');

                if (hasSpaces && hasTabs)
                    mixedCount++;
                else if (hasSpaces)
                    spacesCount++;
                else if (hasTabs)
                    tabsCount++;
            }

            string recommendation = mixedCount > 0 
                ? "Mixed indentation detected - should standardize"
                : spacesCount > tabsCount 
                    ? "Predominantly spaces - continue using spaces"
                    : "Predominantly tabs - continue using tabs";

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                analysis = new
                {
                    linesWithSpaces = spacesCount,
                    linesWithTabs = tabsCount,
                    linesWithMixed = mixedCount,
                    recommendation
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing indentation: {Path}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("cleanup_backup_files")]
    [Description("Clean up backup files in a directory based on age and pattern")]
    public Task<string> CleanupBackupFiles(
        [Description("Directory path to clean")] string directoryPath,
        [Description("Delete backups older than this many hours (0 = all)")] int olderThanHours = 24,
        [Description("File pattern to match (default: '*.backup.*')")] string pattern = "*.backup.*")
    {
        try
        {
            directoryPath = Path.GetFullPath(directoryPath);
            
            if (!Directory.Exists(directoryPath))
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Directory not found" }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            DateTime cutoffTime = DateTime.Now.AddHours(-olderThanHours);
            List<string> backupFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories)
                .Where(f => olderThanHours == 0 || File.GetLastWriteTime(f) < cutoffTime)
                .ToList();

            foreach (string file in backupFiles)
            {
                File.Delete(file);
            }

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                directoryPath,
                filesDeleted = backupFiles.Count,
                message = $"Deleted {backupFiles.Count} backup files"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up backups in: {Path}", directoryPath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }
}