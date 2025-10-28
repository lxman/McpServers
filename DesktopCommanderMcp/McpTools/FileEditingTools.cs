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
    [Description("PHASE 1: Prepare line replacement. See file-editing/SKILL.md")]
    public async Task<string> PrepareReplaceLines(
        string filePath,
        int startLine,
        int endLine,
        string newContent,
        string versionToken,
        bool createBackup = false)
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
    [Description("PHASE 1: Prepare content insertion. See file-editing/SKILL.md")]
    public async Task<string> PrepareInsertAfterLine(
        string filePath,
        int afterLine,
        string content,
        string versionToken,
        bool maintainIndentation = true,
        bool createBackup = false)
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
    [Description("PHASE 1: Prepare line deletion. See file-editing/SKILL.md")]
    public async Task<string> PrepareDeleteLines(
        string filePath,
        int startLine,
        int endLine,
        string versionToken,
        bool createBackup = false)
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
    [Description("PHASE 1: Prepare text pattern replacement. See file-editing/SKILL.md")]
    public async Task<string> PrepareReplaceInFile(
        string filePath,
        string searchPattern,
        string replaceWith,
        string versionToken,
        bool caseSensitive = false,
        bool useRegex = false,
        bool createBackup = false)
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
    [Description("PHASE 2: Apply pending edit (requires 'APPROVE'). See file-editing/SKILL.md")]
    public async Task<string> ApproveEdit(
        string approvalToken,
        string confirmation)
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
    [Description("Cancel pending edit. See file-editing/SKILL.md")]
    public Task<string> CancelEdit(
        string approvalToken)
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
    [Description("List pending edits awaiting approval. See file-editing/SKILL.md")]
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
    [Description("Find lines matching pattern. See file-operations/SKILL.md")]
    public async Task<string> FindInFile(
        string filePath,
        string pattern,
        bool caseSensitive = false,
        bool useRegex = false)
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
    [Description("Analyze file indentation patterns. See file-operations/SKILL.md")]
    public async Task<string> AnalyzeIndentation(
        string filePath)
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
    [Description("Clean up backup files by age/pattern. See maintenance/SKILL.md")]
    public Task<string> CleanupBackupFiles(
        string directoryPath,
        int olderThanHours = 24,
        string pattern = "*.backup.*")
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