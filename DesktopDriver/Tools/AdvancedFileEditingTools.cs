using System.ComponentModel;
using DesktopDriver.Services;
using DesktopDriver.Services.AdvancedFileEditing;
using DesktopDriver.Services.AdvancedFileEditing.Models;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class AdvancedFileEditingTools(
    SecurityManager securityManager, 
    AuditLogger auditLogger,
    FileEditor fileEditor)
{
    [McpServerTool]
    [Description("Replace a range of lines in a file with new content")]
    public async Task<string> ReplaceFileLines(
        [Description("Path to the file to edit")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (1-based, inclusive)")] int endLine,
        [Description("New content to replace the lines with")] string newContent,
        [Description("Create backup before editing (default: true)")] bool createBackup = true)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ReplaceLines", fullPath, false, error);
                return error;
            }

            EditResult result = await fileEditor.ReplaceFileLines(fullPath, startLine, endLine, newContent, createBackup);
            
            auditLogger.LogFileOperation("ReplaceLines", fullPath, result.Success, 
                result.Success ? null : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("ReplaceLines", filePath, false, ex.Message);
            return $"Error replacing lines: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Insert content after a specific line in a file")]
    public async Task<string> InsertAfterLine(
        [Description("Path to the file to edit")] string filePath,
        [Description("Line number to insert after (0 = beginning of file)")] int afterLine,
        [Description("Content to insert")] string content,
        [Description("Automatically maintain proper indentation (default: true)")] bool maintainIndentation = true,
        [Description("Create backup before editing (default: true)")] bool createBackup = true)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("InsertAfterLine", fullPath, false, error);
                return error;
            }

            EditResult result = await fileEditor.InsertAfterLine(fullPath, afterLine, content, maintainIndentation, createBackup);
            
            auditLogger.LogFileOperation("InsertAfterLine", fullPath, result.Success, 
                result.Success ? null : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("InsertAfterLine", filePath, false, ex.Message);
            return $"Error inserting content: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Delete a range of lines from a file")]
    public async Task<string> DeleteFileLines(
        [Description("Path to the file to edit")] string filePath,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (1-based, inclusive)")] int endLine,
        [Description("Create backup before editing (default: true)")] bool createBackup = true)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("DeleteLines", fullPath, false, error);
                return error;
            }

            EditResult result = await fileEditor.DeleteLines(fullPath, startLine, endLine, createBackup);
            
            auditLogger.LogFileOperation("DeleteLines", fullPath, result.Success, 
                result.Success ? null : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("DeleteLines", filePath, false, ex.Message);
            return $"Error deleting lines: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Replace text patterns within a file using string matching or regular expressions")]
    public async Task<string> ReplaceInFile(
        [Description("Path to the file to edit")] string filePath,
        [Description("Text pattern to search for")] string searchPattern,
        [Description("Replacement text")] string replaceWith,
        [Description("Use regular expressions for pattern matching")] bool useRegex = false,
        [Description("Case-sensitive matching")] bool caseSensitive = false,
        [Description("Create backup before editing (default: true)")] bool createBackup = true)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("ReplaceInFile", fullPath, false, error);
                return error;
            }

            EditResult result = await fileEditor.ReplaceInFile(fullPath, searchPattern, replaceWith, useRegex, caseSensitive, createBackup);
            
            auditLogger.LogFileOperation("ReplaceInFile", fullPath, result.Success, 
                result.Success ? null : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("ReplaceInFile", filePath, false, ex.Message);
            return $"Error replacing text: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Preview what changes would be made without actually modifying the file")]
    public async Task<string> PreviewFileEdit(
        [Description("Path to the file to preview")] string filePath,
        [Description("Type of operation: Replace, Insert, or Delete")] string operationType,
        [Description("Starting line number (1-based)")] int startLine,
        [Description("Ending line number (1-based, for Replace/Delete) or unused for Insert")] int endLine,
        [Description("Content for Replace/Insert operations")] string content = "")
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!securityManager.IsDirectoryAllowed(Path.GetDirectoryName(fullPath)!))
            {
                var error = $"Access denied to directory: {Path.GetDirectoryName(fullPath)}";
                auditLogger.LogFileOperation("PreviewEdit", fullPath, false, error);
                return error;
            }

            EditOperation operation = operationType.ToLowerInvariant() switch
            {
                "replace" => EditOperation.Replace(startLine, endLine, content, $"Replace lines {startLine}-{endLine}"),
                "insert" => EditOperation.Insert(startLine, content, $"Insert after line {startLine}"),
                "delete" => EditOperation.Delete(startLine, endLine, $"Delete lines {startLine}-{endLine}"),
                _ => throw new ArgumentException($"Invalid operation type: {operationType}")
            };

            EditResult result = await fileEditor.PreviewEdit(fullPath, operation);
            
            auditLogger.LogFileOperation("PreviewEdit", fullPath, result.Success, 
                result.Success ? null : result.ErrorDetails);
            
            return result.FormatForUser();
        }
        catch (Exception ex)
        {
            auditLogger.LogFileOperation("PreviewEdit", filePath, false, ex.Message);
            return $"Error previewing edit: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Find lines in a file that match a specific pattern")]
    public async Task<string> FindInFile(
        [Description("Path to the file to search")] string filePath,
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

            (bool success, int[] lineNumbers, string? errorMessage) = await fileEditor.FindInFile(fullPath, pattern, useRegex, caseSensitive);
            
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
        [Description("Path to the file to analyze")] string filePath)
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

            (bool isConsistent, string analysis) = await fileEditor.AnalyzeIndentation(fullPath);
            
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
}