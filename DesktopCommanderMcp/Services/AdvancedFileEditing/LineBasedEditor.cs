using System.Text.RegularExpressions;
using DesktopCommanderMcp.Services.AdvancedFileEditing.Models;

namespace DesktopCommanderMcp.Services.AdvancedFileEditing;

public class LineBasedEditor(DiffPatchService diffPatchService, IndentationManager indentationManager)
{
    /// <summary>
    /// Replaces a range of lines with new content
    /// </summary>
    public static (bool success, string[] newLines, string? errorMessage) ReplaceLines(
        string[] originalLines, 
        int startLine, 
        int endLine, 
        string newContent)
    {
        try
        {
            var range = new LineRange(startLine, endLine);
            if (!range.IsWithinFile(originalLines.Length))
            {
                return (false, originalLines, $"Line range {range} is outside file bounds (1-{originalLines.Length})");
            }
            
            EditOperation operation = EditOperation.Replace(startLine, endLine, newContent);
            (bool isValid, string? validationError) = DiffPatchService.ValidateEdit(originalLines, operation);
            
            if (!isValid)
                return (false, originalLines, validationError);
            
            string[] result = ApplyOperation(originalLines, operation);
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, originalLines, $"Error replacing lines: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Inserts content after the specified line
    /// </summary>
    public static (bool success, string[] newLines, string? errorMessage) InsertAfterLine(
        string[] originalLines, 
        int afterLine, 
        string content)
    {
        try
        {
            if (afterLine < 0 || afterLine > originalLines.Length)
            {
                return (false, originalLines, $"Insert position {afterLine} is outside valid range (0-{originalLines.Length})");
            }
            
            EditOperation operation = EditOperation.Insert(afterLine, content);
            (bool isValid, string? validationError) = DiffPatchService.ValidateEdit(originalLines, operation);
            
            if (!isValid)
                return (false, originalLines, validationError);
            
            string[] result = ApplyOperation(originalLines, operation);
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, originalLines, $"Error inserting lines: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Inserts content with proper indentation matching the insertion context
    /// </summary>
    public (bool success, string[] newLines, string? errorMessage) InsertWithIndentation(
        string[] originalLines, 
        int afterLine, 
        string content)
    {
        try
        {
            // Detect the file's indentation style
            IndentationInfo fileIndentation = IndentationManager.DetectFileIndentation(originalLines);
            
            // Determine the appropriate indentation level for the insertion point
            int targetLevel = IndentationManager.DetermineInsertionIndentLevel(originalLines, afterLine);
            
            // Fix the indentation of the content to match
            string indentedContent = IndentationManager.FixIndentation(content, fileIndentation, targetLevel);
            
            return InsertAfterLine(originalLines, afterLine, indentedContent);
        }
        catch (Exception ex)
        {
            return (false, originalLines, $"Error inserting with indentation: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Deletes a range of lines
    /// </summary>
    public static (bool success, string[] newLines, string? errorMessage) DeleteLines(
        string[] originalLines, 
        int startLine, 
        int endLine)
    {
        try
        {
            var range = new LineRange(startLine, endLine);
            if (!range.IsWithinFile(originalLines.Length))
            {
                return (false, originalLines, $"Line range {range} is outside file bounds (1-{originalLines.Length})");
            }
            
            EditOperation operation = EditOperation.Delete(startLine, endLine);
            (bool isValid, string? validationError) = DiffPatchService.ValidateEdit(originalLines, operation);
            
            if (!isValid)
                return (false, originalLines, validationError);
            
            string[] result = ApplyOperation(originalLines, operation);
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, originalLines, $"Error deleting lines: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Finds lines matching a pattern and returns their line numbers
    /// </summary>
    public static int[] FindLines(string[] lines, string pattern, bool useRegex = false, bool caseSensitive = false)
    {
        var matches = new List<int>();
        StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        
        for (var i = 0; i < lines.Length; i++)
        {
            bool isMatch;
            
            if (useRegex)
            {
                try
                {
                    var regex = new Regex(pattern, 
                        caseSensitive ? RegexOptions.None : 
                                       RegexOptions.IgnoreCase);
                    isMatch = regex.IsMatch(lines[i]);
                }
                catch
                {
                    isMatch = false; // Invalid regex, treat as no match
                }
            }
            else
            {
                isMatch = lines[i].Contains(pattern, comparison);
            }
            
            if (isMatch)
                matches.Add(i + 1); // Convert to 1-based line numbers
        }
        
        return matches.ToArray();
    }
    
    /// <summary>
    /// Replaces content on lines matching a specific pattern
    /// </summary>
    public static (bool success, string[] newLines, string? errorMessage) ReplaceInLines(
        string[] originalLines, 
        string searchPattern, 
        string replaceWith, 
        bool useRegex = false, 
        bool caseSensitive = false)
    {
        try
        {
            List<string> result = [..originalLines];
            var replacementCount = 0;
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            
            for (var i = 0; i < result.Count; i++)
            {
                string newLine;
                
                if (useRegex)
                {
                    try
                    {
                        var regex = new Regex(searchPattern,
                            caseSensitive ? RegexOptions.None :
                                           RegexOptions.IgnoreCase);
                        newLine = regex.Replace(result[i], replaceWith);
                    }
                    catch (Exception ex)
                    {
                        return (false, originalLines, $"Invalid regex pattern: {ex.Message}");
                    }
                }
                else
                {
                    newLine = result[i].Replace(searchPattern, replaceWith, comparison);
                }
                
                if (newLine != result[i])
                {
                    result[i] = newLine;
                    replacementCount++;
                }
            }
            
            if (replacementCount == 0)
            {
                return (false, originalLines, $"Pattern '{searchPattern}' not found in any lines");
            }
            
            return (true, result.ToArray(), null);
        }
        catch (Exception ex)
        {
            return (false, originalLines, $"Error during replacement: {ex.Message}");
        }
    }
    
    private static string[] ApplyOperation(string[] originalLines, EditOperation operation)
    {
        List<string> result = [..originalLines];
        
        switch (operation.Type)
        {
            case EditOperationType.Replace:
                int removeCount = operation.EndLine - operation.StartLine + 1;
                result.RemoveRange(operation.StartLine - 1, removeCount);
                
                if (!string.IsNullOrEmpty(operation.Content))
                {
                    string[] newLines = SplitContent(operation.Content);
                    result.InsertRange(operation.StartLine - 1, newLines);
                }
                break;
                
            case EditOperationType.Insert:
                if (!string.IsNullOrEmpty(operation.Content))
                {
                    string[] newLines = SplitContent(operation.Content);
                    result.InsertRange(operation.StartLine, newLines);
                }
                break;
                
            case EditOperationType.Delete:
                int deleteCount = operation.EndLine - operation.StartLine + 1;
                result.RemoveRange(operation.StartLine - 1, deleteCount);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unsupported operation type: {operation.Type}");
        }
        
        return result.ToArray();
    }
    
    private static string[] SplitContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return [];
            
        return content.Split(['\n', '\r'], StringSplitOptions.None)
                     .Where(line => !line.Equals("\r"))
                     .ToArray();
    }
}