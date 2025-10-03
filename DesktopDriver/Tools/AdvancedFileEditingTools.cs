using System.ComponentModel;
using DesktopDriver.Exceptions;
using DesktopDriver.Services;
using DesktopDriver.Services.AdvancedFileEditing;
using DesktopDriver.Services.AdvancedFileEditing.Models;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class AdvancedFileEditingTools(
    SecurityManager securityManager, 
    AuditLogger auditLogger,
    FileEditor fileEditor,
    FileVersionService versionService,
    EditApprovalService approvalService)
{
    [McpServerTool]
    [Description("""
                 PHASE 1: Prepare to replace a range of lines in a file with new content.
                 
                 ⚠️ TWO-PHASE EDIT PROTOCOL:
                 1. Read the file first using read_file or advanced_file_read_range to get version_token
                 2. Call this tool with the version_token - it will prepare the edit and show FULL FILE PREVIEW
                 3. Review the complete file preview that is returned
                 4. Call approve_file_edit with the approval_token to apply the changes
                 
                 CRITICAL: The edit is NOT applied until you call approve_file_edit!
                 You will receive the complete file content with edits applied for review.
                 """)]
    public async Task<string> ReplaceFileLines(
        [Description("Path to the file to edit - must be canonical")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (1-based, inclusive)")] int endLine,
        [Description("New content to replace the lines with")] string newContent,
        [Description("Version token from previous read operation (REQUIRED)")] string versionToken,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("PrepareReplaceLines", fullPath, false, error);
                return error;
            }

            // Validate version token
            try
            {
                versionService.ValidateVersionTokenOrThrow(fullPath, versionToken);
            }
            catch (FileConflictException ex)
            {
                auditLogger.LogFileOperation("PrepareReplaceLines", fullPath, false, "FILE_CONFLICT");
                return $"❌ {ex.Message}";
            }

            EditResult result = await fileEditor.PrepareReplaceFileLines(
                fullPath, startLine, endLine, newContent, versionToken, createBackup);
            
            auditLogger.LogFileOperation("PrepareReplaceLines", fullPath, result.Success, 
                result.Success ? "Preview created" : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("PrepareReplaceLines", filePath, false, ex.Message);
            return $"Error preparing edit: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("""
                 PHASE 1: Prepare to insert content after a specific line in a file.
                 
                 ⚠️ TWO-PHASE EDIT PROTOCOL:
                 1. Read the file first using read_file or advanced_file_read_range to get version_token
                 2. Call this tool with the version_token - it will prepare the edit and show FULL FILE PREVIEW
                 3. Review the complete file preview that is returned
                 4. Call approve_file_edit with the approval_token to apply the changes
                 
                 CRITICAL: The edit is NOT applied until you call approve_file_edit!
                 You will receive the complete file content with edits applied for review.
                 """)]
    public async Task<string> InsertAfterLine(
        [Description("Path to the file to edit - must be canonical")] string filePath,
        [Description("Line number to insert after (0 = beginning of file)")] int afterLine,
        [Description("Content to insert")] string content,
        [Description("Version token from previous read operation (REQUIRED)")] string versionToken,
        [Description("Automatically maintain proper indentation (default: true)")] bool maintainIndentation = true,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("PrepareInsertAfterLine", fullPath, false, error);
                return error;
            }

            // Validate version token
            try
            {
                versionService.ValidateVersionTokenOrThrow(fullPath, versionToken);
            }
            catch (FileConflictException ex)
            {
                auditLogger.LogFileOperation("PrepareInsertAfterLine", fullPath, false, "FILE_CONFLICT");
                return $"❌ {ex.Message}";
            }

            EditResult result = await fileEditor.PrepareInsertAfterLine(
                fullPath, afterLine, content, versionToken, maintainIndentation, createBackup);
            
            auditLogger.LogFileOperation("PrepareInsertAfterLine", fullPath, result.Success, 
                result.Success ? "Preview created" : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("PrepareInsertAfterLine", filePath, false, ex.Message);
            return $"Error preparing edit: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("""
                 PHASE 1: Prepare to delete a range of lines from a file.
                 
                 ⚠️ TWO-PHASE EDIT PROTOCOL:
                 1. Read the file first using read_file or advanced_file_read_range to get version_token
                 2. Call this tool with the version_token - it will prepare the edit and show FULL FILE PREVIEW
                 3. Review the complete file preview that is returned
                 4. Call approve_file_edit with the approval_token to apply the changes
                 
                 CRITICAL: The edit is NOT applied until you call approve_file_edit!
                 You will receive the complete file content with edits applied for review.
                 """)]
    public async Task<string> DeleteFileLines(
        [Description("Path to the file to edit - must be canonical")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (1-based, inclusive)")] int endLine,
        [Description("Version token from previous read operation (REQUIRED)")] string versionToken,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("PrepareDeleteLines", fullPath, false, error);
                return error;
            }

            // Validate version token
            try
            {
                versionService.ValidateVersionTokenOrThrow(fullPath, versionToken);
            }
            catch (FileConflictException ex)
            {
                auditLogger.LogFileOperation("PrepareDeleteLines", fullPath, false, "FILE_CONFLICT");
                return $"❌ {ex.Message}";
            }

            EditResult result = await fileEditor.PrepareDeleteLines(
                fullPath, startLine, endLine, versionToken, createBackup);
            
            auditLogger.LogFileOperation("PrepareDeleteLines", fullPath, result.Success, 
                result.Success ? "Preview created" : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("PrepareDeleteLines", filePath, false, ex.Message);
            return $"Error preparing edit: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("""
                 PHASE 1: Prepare to replace text patterns within a file.
                 
                 ⚠️ TWO-PHASE EDIT PROTOCOL:
                 1. Read the file first to verify the search pattern exists and get version_token
                 2. Use find_in_file to locate exact matches before replacing
                 3. Call this tool with the version_token - it will prepare the edit and show FULL FILE PREVIEW
                 4. Review the complete file preview that is returned
                 5. Call approve_file_edit with the approval_token to apply the changes
                 
                 CRITICAL: The edit is NOT applied until you call approve_file_edit!
                 You will receive the complete file content with edits applied for review.
                 """)]
    public async Task<string> ReplaceInFile(
        [Description("Path to the file to edit - must be canonical")] string filePath,
        [Description("Text pattern to search for")] string searchPattern,
        [Description("Replacement text")] string replaceWith,
        [Description("Version token from previous read operation (REQUIRED)")] string versionToken,
        [Description("Use regular expressions for pattern matching")] bool useRegex = false,
        [Description("Case-sensitive matching")] bool caseSensitive = false,
        [Description("Create backup before editing (default: false)")] bool createBackup = false)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("PrepareReplaceInFile", fullPath, false, error);
                return error;
            }

            // Validate version token
            try
            {
                versionService.ValidateVersionTokenOrThrow(fullPath, versionToken);
            }
            catch (FileConflictException ex)
            {
                auditLogger.LogFileOperation("PrepareReplaceInFile", fullPath, false, "FILE_CONFLICT");
                return $"❌ {ex.Message}";
            }

            EditResult result = await fileEditor.PrepareReplaceInFile(
                fullPath, searchPattern, replaceWith, versionToken, useRegex, caseSensitive, createBackup);
            
            auditLogger.LogFileOperation("PrepareReplaceInFile", fullPath, result.Success, 
                result.Success ? "Preview created" : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("PrepareReplaceInFile", filePath, false, ex.Message);
            return $"Error preparing edit: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("""
                 PHASE 2: Apply a pending edit after reviewing the preview.
                 
                 ⚠️ CRITICAL - THIS APPLIES THE EDIT TO THE FILE:
                 1. You must have called one of the edit preparation tools (replace_file_lines, insert_after_line, etc.)
                 2. You must have reviewed the FULL FILE PREVIEW that was returned
                 3. You must pass the approval_token from the preview
                 4. You must pass confirmation="APPROVE" to confirm you reviewed the preview
                 5. This will ACTUALLY MODIFY THE FILE
                 
                 After approval, you'll receive a new version_token for subsequent edits.
                 """)]
    public async Task<string> ApproveFileEdit(
        [Description("Approval token from the preview operation (REQUIRED)")] string approvalToken,
        [Description("Must be exactly 'APPROVE' to confirm you reviewed the preview (REQUIRED)")] string confirmation)
    {
        try
        {
            // Require explicit confirmation
            if (confirmation != "APPROVE")
            {
                return "❌ Edit approval denied: You must pass confirmation='APPROVE' to confirm you reviewed the preview.";
            }

            // Get the pending edit (this consumes it)
            IReadOnlyList<PendingEdit> pendingEdits = approvalService.GetAllPendingEdits();
            PendingEdit? pendingEdit = pendingEdits.FirstOrDefault(pe => pe.ApprovalToken == approvalToken);
            
            if (pendingEdit == null)
            {
                auditLogger.LogFileOperation("ApproveEdit", "unknown", false, "Invalid or expired approval token");
                return "❌ Invalid or expired approval token. Approval tokens expire after 5 minutes.";
            }

            string fullPath = Path.GetFullPath(pendingEdit.FilePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ApproveEdit", fullPath, false, error);
                return error;
            }

            // Get current version token to validate file hasn't changed
            string currentVersionToken = versionService.ComputeVersionToken(fullPath);
            
            EditResult result = await fileEditor.ApplyPendingEdit(approvalToken, currentVersionToken);
            
            auditLogger.LogFileOperation("ApproveEdit", fullPath, result.Success, 
                result.Success ? "Edit applied successfully" : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("ApproveEdit", "unknown", false, ex.Message);
            return $"Error applying edit: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List all pending edits awaiting approval")]
    public string ListPendingEdits()
    {
        try
        {
            IReadOnlyList<PendingEdit> pendingEdits = approvalService.GetAllPendingEdits();
            
            if (pendingEdits.Count == 0)
            {
                return "✅ No pending edits awaiting approval.";
            }

            var result = $"📋 {pendingEdits.Count} Pending Edit(s) Awaiting Approval:\n\n";
            
            foreach (PendingEdit edit in pendingEdits)
            {
                result += $"🔐 Approval Token: {edit.ApprovalToken}\n";
                result += $"   File: {edit.FilePath}\n";
                result += $"   Operation: {edit.Operation.Description}\n";
                result += $"   Lines Affected: {edit.LinesAffected}\n";
                result += $"   Expires: {edit.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC\n";
                result += $"   Backup: {(edit.CreateBackup ? "Yes" : "No")}\n\n";
            }
            
            result += "⚠️ Use approve_file_edit with the approval token to apply changes.";
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error listing pending edits: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Cancel a pending edit by approval token")]
    public string CancelPendingEdit(
        [Description("Approval token of the edit to cancel")] string approvalToken)
    {
        try
        {
            bool cancelled = approvalService.CancelPendingEdit(approvalToken);
            
            if (cancelled)
            {
                auditLogger.LogFileOperation("CancelEdit", "unknown", true, "Edit cancelled");
                return $"✅ Pending edit cancelled successfully: {approvalToken}";
            }
            else
            {
                return $"❌ Approval token not found or already expired: {approvalToken}";
            }
        }
        catch (Exception ex)
        {
            return $"Error cancelling edit: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Find lines in a file that match a specific pattern")]
    public async Task<string> FindInFile(
        [Description("Path to the file to search - must be canonical")] string filePath,
        [Description("Pattern to search for")] string pattern,
        [Description("Use regular expressions for pattern matching")] bool useRegex = false,
        [Description("Case-sensitive matching")] bool caseSensitive = false)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("FindInFile", fullPath, false, error);
                return error;
            }

            (bool success, int[] lineNumbers, string? errorMessage) = await FileEditor.FindInFile(fullPath, pattern, useRegex, caseSensitive);
            
            auditLogger.LogFileOperation("FindInFile", fullPath, success, errorMessage);
            
            if (!success)
                return $"❌ Search failed: {errorMessage}";

            if (lineNumbers.Length == 0)
                return $"🔍 No matches found for pattern '{pattern}' in file: {fullPath}";

            var result = $"🔍 Found {lineNumbers.Length} matches for pattern '{pattern}' in file: {fullPath}\n";
            result += $"Line numbers: {string.Join(", ", lineNumbers)}";
            
            return result;
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("FindInFile", filePath, false, ex.Message);
            return $"Error searching file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Analyze indentation consistency in a file and provide recommendations")]
    public async Task<string> AnalyzeIndentation(
        [Description("Path to the file to analyze - must be canonical")] string filePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("AnalyzeIndentation", fullPath, false, error);
                return error;
            }

            (bool isConsistent, string analysis) = await FileEditor.AnalyzeIndentation(fullPath);
            
            auditLogger.LogFileOperation("AnalyzeIndentation", fullPath, true);
            
            string icon = isConsistent ? "✅" : "⚠️";
            return $"{icon} Indentation Analysis for: {fullPath}\n\n{analysis}";
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("AnalyzeIndentation", filePath, false, ex.Message);
            return $"Error analyzing indentation: {ex.Message}";
        }
    }
    
    [McpServerTool]
    [Description("Clean up backup files older than specified hours or all backups for a directory")]
    public string CleanupBackupFiles(
        [Description("Directory path to clean - must be canonical")] string directoryPath,
        [Description("Delete backups older than this many hours (0 = delete all)")] int olderThanHours = 24,
        [Description("File pattern to match (default: *.backup.*)")] string pattern = "*.backup.*")
    {
        try
        {
            string fullPath = Path.GetFullPath(directoryPath);
            if (!securityManager.IsDirectoryAllowed(fullPath))
            {
                var error = $"Access denied to directory: {fullPath}";
                auditLogger.LogFileOperation("CleanupBackups", fullPath, false, error);
                return error;
            }

            if (!Directory.Exists(fullPath))
                return $"Directory not found: {fullPath}";

            string[] backupFiles = Directory.GetFiles(fullPath, pattern, SearchOption.TopDirectoryOnly);
            DateTime cutoffTime = DateTime.Now.AddHours(-olderThanHours);
            var deletedCount = 0;
            var totalSize = 0L;

            foreach (string backupFile in backupFiles)
            {
                var fileInfo = new FileInfo(backupFile);

                if (olderThanHours != 0 && fileInfo.LastWriteTime >= cutoffTime) continue;
                totalSize += fileInfo.Length;
                File.Delete(backupFile);
                deletedCount++;
            }

            string sizeText = totalSize > 1024 * 1024 
                ? $"{totalSize / (1024 * 1024.0):F1} MB" 
                : $"{totalSize / 1024.0:F1} KB";

            auditLogger.LogFileOperation("CleanupBackups", fullPath, true, $"Deleted {deletedCount} files ({sizeText})");
            
            return deletedCount > 0 
                ? $"🧹 Cleaned up {deletedCount} backup files ({sizeText}) from: {fullPath}"
                : $"✅ No backup files found to clean up in: {fullPath}";
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("CleanupBackups", directoryPath, false, ex.Message);
            return $"Error cleaning up backups: {ex.Message}";
        }
    }
}