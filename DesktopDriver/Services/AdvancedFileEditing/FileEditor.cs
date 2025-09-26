using DesktopDriver.Services.AdvancedFileEditing.Models;

namespace DesktopDriver.Services.AdvancedFileEditing;

public class FileEditor(
    LineBasedEditor lineBasedEditor,
    DiffPatchService diffPatchService,
    IndentationManager indentationManager)
{
    private readonly HashSet<string> _backedUpFilesThisSession = [];
    
    /// <summary>
    /// Replaces a range of lines in a file with new content
    /// </summary>
    public async Task<EditResult> ReplaceFileLines(string filePath, int startLine, int endLine, string newContent, bool createBackup = false)
    {
        try
        {
            if (!File.Exists(filePath))
                return EditResult.CreateFailure(filePath, "File not found");
            
            string[] originalLines = await File.ReadAllLinesAsync(filePath);
            string originalContent = string.Join('\n', originalLines);
            
            (bool success, string[] newLines, string? errorMessage) = lineBasedEditor.ReplaceLines(originalLines, startLine, endLine, newContent);
            
            if (!success)
                return EditResult.CreateFailure(filePath, errorMessage ?? "Unknown error occurred");
            
            if (createBackup) CreateBackupIfNeeded(filePath, createBackup);
            
            await File.WriteAllLinesAsync(filePath, newLines);
            
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, string.Join('\n', newLines), filePath);
            int linesAffected = Math.Abs(newLines.Length - originalLines.Length) + (endLine - startLine + 1);
            
            return EditResult.CreateSuccess(filePath, linesAffected, 
                $"Successfully replaced lines {startLine}-{endLine}", diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "File operation failed", ex.Message);
        }
    }
    
    /// <summary>
    /// Inserts content after the specified line in a file
    /// </summary>
    public async Task<EditResult> InsertAfterLine(string filePath, int afterLine, string content, bool maintainIndentation = true, bool createBackup = false)
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
            
            if (createBackup) CreateBackupIfNeeded(filePath, createBackup);
            
            await File.WriteAllLinesAsync(filePath, newLines);
            
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, string.Join('\n', newLines), filePath);
            int linesInserted = newLines.Length - originalLines.Length;
            
            return EditResult.CreateSuccess(filePath, linesInserted, 
                $"Successfully inserted {linesInserted} lines after line {afterLine}", diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "File operation failed", ex.Message);
        }
    }
    
    /// <summary>
    /// Deletes a range of lines from a file
    /// </summary>
    public async Task<EditResult> DeleteLines(string filePath, int startLine, int endLine, bool createBackup = false)
    {
        try
        {
            if (!File.Exists(filePath))
                return EditResult.CreateFailure(filePath, "File not found");
            
            string[] originalLines = await File.ReadAllLinesAsync(filePath);
            string originalContent = string.Join('\n', originalLines);
            
            (bool success, string[] newLines, string? errorMessage) = lineBasedEditor.DeleteLines(originalLines, startLine, endLine);
            
            if (!success)
                return EditResult.CreateFailure(filePath, errorMessage ?? "Unknown error occurred");
            
            if (createBackup) CreateBackupIfNeeded(filePath, createBackup);
            
            await File.WriteAllLinesAsync(filePath, newLines);
            
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, string.Join('\n', newLines), filePath);
            int linesDeleted = originalLines.Length - newLines.Length;
            
            return EditResult.CreateSuccess(filePath, linesDeleted, 
                $"Successfully deleted lines {startLine}-{endLine} ({linesDeleted} lines)", diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "File operation failed", ex.Message);
        }
    }
    
    /// <summary>
    /// Replaces text patterns within a file
    /// </summary>
    public async Task<EditResult> ReplaceInFile(string filePath, string searchPattern, string replaceWith, 
        bool useRegex = false, bool caseSensitive = false, bool createBackup = false)
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
            
            if (createBackup) CreateBackupIfNeeded(filePath, createBackup);
            
            await File.WriteAllLinesAsync(filePath, newLines);
            
            string diff = diffPatchService.GenerateUnifiedDiff(originalContent, string.Join('\n', newLines), filePath);
            int changedLines = CountChangedLines(originalLines, newLines);
            
            return EditResult.CreateSuccess(filePath, changedLines, 
                $"Successfully replaced '{searchPattern}' with '{replaceWith}' on {changedLines} lines", diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "File operation failed", ex.Message);
        }
    }
    
    /// <summary>
    /// Previews what changes would be made without actually modifying the file
    /// </summary>
    public async Task<EditResult> PreviewEdit(string filePath, EditOperation operation)
    {
        try
        {
            if (!File.Exists(filePath))
                return EditResult.CreateFailure(filePath, "File not found");
            
            string[] originalLines = await File.ReadAllLinesAsync(filePath);
            (bool isValid, string? errorMessage) = diffPatchService.ValidateEdit(originalLines, operation);
            
            if (!isValid)
                return EditResult.CreateFailure(filePath, errorMessage ?? "Invalid operation");
            
            string diff = diffPatchService.CreateEditPreview(originalLines, operation);
            
            return EditResult.CreateSuccess(filePath, 0, 
                $"Preview of {operation.Type} operation on {operation.Description}", diff);
        }
        catch (Exception ex)
        {
            return EditResult.CreateFailure(filePath, "Preview failed", ex.Message);
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