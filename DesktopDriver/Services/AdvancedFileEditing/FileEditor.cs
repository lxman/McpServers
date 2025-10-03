using DesktopDriver.Services.AdvancedFileEditing.Models;

namespace DesktopDriver.Services.AdvancedFileEditing;

public class FileEditor(
    LineBasedEditor lineBasedEditor,
    DiffPatchService diffPatchService,
    IndentationManager indentationManager,
    EditApprovalService approvalService,
    FileVersionService versionService)
{
    private readonly HashSet<string> _backedUpFilesThisSession = [];
    
    /// <summary>
    /// PHASE 1: Prepares a replace operation and returns preview with approval token
    /// </summary>
    public async Task<EditResult> PrepareReplaceFileLines(
        string filePath, 
        int startLine, 
        int endLine, 
        string newContent,
        string originalVersionToken,
        bool createBackup = false)
    {
        try
        {
            if (!File.Exists(filePath))
                return EditResult.CreateFailure(filePath, "File not found");
            
            string[] originalLines = await File.ReadAllLinesAsync(filePath);
            string originalContent = string.Join('\n', originalLines);
            
            (bool success, string[] newLines, string? errorMessage) = 
                lineBasedEditor.ReplaceLines(originalLines, startLine, endLine, newContent);
            
            if (!success)
                return EditResult.CreateFailure(filePath, errorMessage ?? "Unknown error occurred");
            
            string previewContent = string.Join('\n', newLines);
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, previewContent, filePath);
            int linesAffected = Math.Abs(newLines.Length - originalLines.Length) + (endLine - startLine + 1);
            
            EditOperation operation = EditOperation.Replace(startLine, endLine, newContent, 
                $"Replace lines {startLine}-{endLine}");
            
            PendingEdit pendingEdit = approvalService.CreatePendingEdit(
                filePath,
                operation,
                originalVersionToken,
                previewContent,
                diff,
                linesAffected,
                createBackup);
            
            return EditResult.CreatePreview(
                filePath,
                linesAffected,
                $"Prepared replacement of lines {startLine}-{endLine}",
                pendingEdit.ApprovalToken,
                pendingEdit.ExpiresAt,
                previewContent,
                diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "Failed to prepare edit", ex.Message);
        }
    }
    
    /// <summary>
    /// PHASE 1: Prepares an insert operation and returns preview with approval token
    /// </summary>
    public async Task<EditResult> PrepareInsertAfterLine(
        string filePath, 
        int afterLine, 
        string content,
        string originalVersionToken,
        bool maintainIndentation = true, 
        bool createBackup = false)
    {
        try
        {
            if (!File.Exists(filePath))
                return EditResult.CreateFailure(filePath, "File not found");
            
            string[] originalLines = await File.ReadAllLinesAsync(filePath);
            string originalContent = string.Join('\n', originalLines);
            
            (bool success, string[] newLines, string? errorMessage) = maintainIndentation 
                ? lineBasedEditor.InsertWithIndentation(originalLines, afterLine, content)
                : lineBasedEditor.InsertAfterLine(originalLines, afterLine, content);
            
            if (!success)
                return EditResult.CreateFailure(filePath, errorMessage ?? "Unknown error occurred");
            
            string previewContent = string.Join('\n', newLines);
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, previewContent, filePath);
            int linesInserted = newLines.Length - originalLines.Length;
            
            EditOperation operation = EditOperation.Insert(afterLine, content, 
                $"Insert after line {afterLine}");
            
            var metadata = new Dictionary<string, object>
            {
                ["maintainIndentation"] = maintainIndentation
            };
            
            PendingEdit pendingEdit = approvalService.CreatePendingEdit(
                filePath,
                operation,
                originalVersionToken,
                previewContent,
                diff,
                linesInserted,
                createBackup,
                metadata);
            
            return EditResult.CreatePreview(
                filePath,
                linesInserted,
                $"Prepared insertion of {linesInserted} lines after line {afterLine}",
                pendingEdit.ApprovalToken,
                pendingEdit.ExpiresAt,
                previewContent,
                diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "Failed to prepare edit", ex.Message);
        }
    }
    
    /// <summary>
    /// PHASE 1: Prepares a delete operation and returns preview with approval token
    /// </summary>
    public async Task<EditResult> PrepareDeleteLines(
        string filePath, 
        int startLine, 
        int endLine,
        string originalVersionToken,
        bool createBackup = false)
    {
        try
        {
            if (!File.Exists(filePath))
                return EditResult.CreateFailure(filePath, "File not found");
            
            string[] originalLines = await File.ReadAllLinesAsync(filePath);
            string originalContent = string.Join('\n', originalLines);
            
            (bool success, string[] newLines, string? errorMessage) = 
                lineBasedEditor.DeleteLines(originalLines, startLine, endLine);
            
            if (!success)
                return EditResult.CreateFailure(filePath, errorMessage ?? "Unknown error occurred");
            
            string previewContent = string.Join('\n', newLines);
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, previewContent, filePath);
            int linesDeleted = originalLines.Length - newLines.Length;
            
            EditOperation operation = EditOperation.Delete(startLine, endLine, 
                $"Delete lines {startLine}-{endLine}");
            
            PendingEdit pendingEdit = approvalService.CreatePendingEdit(
                filePath,
                operation,
                originalVersionToken,
                previewContent,
                diff,
                linesDeleted,
                createBackup);
            
            return EditResult.CreatePreview(
                filePath,
                linesDeleted,
                $"Prepared deletion of lines {startLine}-{endLine} ({linesDeleted} lines)",
                pendingEdit.ApprovalToken,
                pendingEdit.ExpiresAt,
                previewContent,
                diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "Failed to prepare edit", ex.Message);
        }
    }
    
    /// <summary>
    /// PHASE 1: Prepares a replace-in-file operation and returns preview with approval token
    /// </summary>
    public async Task<EditResult> PrepareReplaceInFile(
        string filePath, 
        string searchPattern, 
        string replaceWith,
        string originalVersionToken,
        bool useRegex = false, 
        bool caseSensitive = false, 
        bool createBackup = false)
    {
        try
        {
            if (!File.Exists(filePath))
                return EditResult.CreateFailure(filePath, "File not found");
            
            string[] originalLines = await File.ReadAllLinesAsync(filePath);
            string originalContent = string.Join('\n', originalLines);
            
            (bool success, string[] newLines, string? errorMessage) = LineBasedEditor.ReplaceInLines(
                originalLines, searchPattern, replaceWith, useRegex, caseSensitive);
            
            if (!success)
                return EditResult.CreateFailure(filePath, errorMessage ?? "No replacements made");
            
            string previewContent = string.Join('\n', newLines);
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, previewContent, filePath);
            int changedLines = CountChangedLines(originalLines, newLines);
            
            // Create a pseudo-operation for replace-in-file
            EditOperation operation = EditOperation.Replace(0, 0, replaceWith, 
                $"Replace '{searchPattern}' with '{replaceWith}'");
            
            var metadata = new Dictionary<string, object>
            {
                ["searchPattern"] = searchPattern,
                ["replaceWith"] = replaceWith,
                ["useRegex"] = useRegex,
                ["caseSensitive"] = caseSensitive
            };
            
            PendingEdit pendingEdit = approvalService.CreatePendingEdit(
                filePath,
                operation,
                originalVersionToken,
                previewContent,
                diff,
                changedLines,
                createBackup,
                metadata);
            
            return EditResult.CreatePreview(
                filePath,
                changedLines,
                $"Prepared replacement of '{searchPattern}' with '{replaceWith}' on {changedLines} lines",
                pendingEdit.ApprovalToken,
                pendingEdit.ExpiresAt,
                previewContent,
                diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "Failed to prepare edit", ex.Message);
        }
    }
    
    /// <summary>
    /// PHASE 2: Applies a pending edit after approval
    /// </summary>
    public async Task<EditResult> ApplyPendingEdit(string approvalToken, string currentVersionToken)
    {
        PendingEdit? pendingEdit = approvalService.ConsumePendingEdit(approvalToken);
        
        if (pendingEdit == null)
        {
            return EditResult.CreateFailure(
                "unknown",
                "Invalid or expired approval token",
                "The approval token may have expired (tokens expire after 5 minutes) or been already used.");
        }
        
        try
        {
            // Validate that file hasn't changed since the preview was created
            if (!versionService.ValidateVersionToken(pendingEdit.FilePath, pendingEdit.OriginalVersionToken))
            {
                return EditResult.CreateFailure(
                    pendingEdit.FilePath,
                    "FILE_CONFLICT: File was modified after preview was generated",
                    $"The file has changed since the preview was created. You must re-read the file and create a new preview. " +
                    $"Expected version: {pendingEdit.OriginalVersionToken}, Current version: {currentVersionToken}");
            }
            
            // Create backup if requested
            if (pendingEdit.CreateBackup)
            {
                CreateBackupIfNeeded(pendingEdit.FilePath, true);
            }
            
            // Write the previewed content to file
            await File.WriteAllTextAsync(pendingEdit.FilePath, pendingEdit.PreviewContent);
            
            // CRITICAL: Do NOT return version token to force file re-read before next edit
            return EditResult.CreateSuccess(
                pendingEdit.FilePath,
                pendingEdit.LinesAffected,
                $"Successfully applied {pendingEdit.Operation.Type} operation: {pendingEdit.Operation.Description}",
                pendingEdit.DiffPreview);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(pendingEdit.FilePath, "Failed to apply edit", ex.Message);
        }
    }
    
    /// <summary>
    /// Finds lines in a file matching a pattern
    /// </summary>
    public static async Task<(bool success, int[] lineNumbers, string? errorMessage)> FindInFile(
        string filePath, string pattern, bool useRegex = false, bool caseSensitive = false)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, Array.Empty<int>(), "File not found");
            
            string[] lines = await File.ReadAllLinesAsync(filePath);
            int[] matches = LineBasedEditor.FindLines(lines, pattern, useRegex, caseSensitive);
            
            return (true, matches, null);
        }
        catch (Exception ex)
        {
            return (false, Array.Empty<int>(), ex.Message);
        }
    }
    
    /// <summary>
    /// Validates file indentation and provides recommendations
    /// </summary>
    public static async Task<(bool isConsistent, string analysis)> AnalyzeIndentation(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, "File not found");
            
            string[] lines = await File.ReadAllLinesAsync(filePath);
            IndentationInfo fileIndentation = IndentationManager.DetectFileIndentation(lines);
            string content = string.Join('\n', lines);
            (bool isConsistent, string? issues) = IndentationManager.ValidateIndentation(content, fileIndentation);
            
            var analysis = $"File indentation analysis:\n";
            analysis += $"Detected style: {fileIndentation}\n";
            analysis += $"Consistent: {(isConsistent ? "Yes" : "No")}";
            
            if (!isConsistent && issues != null)
            {
                analysis += $"\nIssues found: {issues}";
            }
            
            return (isConsistent, analysis);
        }
        catch (Exception ex)
        {
            return (false, $"Analysis failed: {ex.Message}");
        }
    }
    
    private void CreateBackupIfNeeded(string filePath, bool createBackup = false)
    {
        if (!createBackup)
            return;
            
        // Only create one backup per file per session
        string normalizedPath = Path.GetFullPath(filePath);
        if (_backedUpFilesThisSession.Contains(normalizedPath))
            return;
            
        var backupPath = $"{filePath}.backup.{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Copy(filePath, backupPath);
        _backedUpFilesThisSession.Add(normalizedPath);
    }
    
    private static int CountChangedLines(string[] original, string[] modified)
    {
        var changes = 0;
        int maxLength = Math.Max(original.Length, modified.Length);
        
        for (var i = 0; i < maxLength; i++)
        {
            string originalLine = i < original.Length ? original[i] : string.Empty;
            string modifiedLine = i < modified.Length ? modified[i] : string.Empty;
            
            if (originalLine != modifiedLine)
                changes++;
        }
        
        return changes;
    }
}