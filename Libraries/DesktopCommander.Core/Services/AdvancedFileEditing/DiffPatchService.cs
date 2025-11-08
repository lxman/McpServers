using DesktopCommander.Core.Services.AdvancedFileEditing.Models;
using DiffPlex;
using DiffPlex.Chunkers;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPlex.Model;

namespace DesktopCommander.Core.Services.AdvancedFileEditing;

public class DiffPatchService
{
    private readonly Differ _differ;
    private readonly SideBySideDiffBuilder _sideBySideDiffBuilder;
    
    public DiffPatchService()
    {
        _differ = new Differ();
        _sideBySideDiffBuilder = new SideBySideDiffBuilder(_differ);
    }
    
    /// <summary>
    /// Generates a unified diff between original and modified content
    /// </summary>
    public string GenerateUnifiedDiff(string originalContent, string modifiedContent, string filePath = "")
    {
        var diff = _differ.CreateDiffs(originalContent, modifiedContent, true, false, LineChunker.Instance);
        var originalLines = SplitLines(originalContent);
        var modifiedLines = SplitLines(modifiedContent);
        return FormatAsUnifiedDiff(diff, filePath, originalLines, modifiedLines);
    }
    
    /// <summary>
    /// Generates a side-by-side diff for better visualization
    /// </summary>
    public SideBySideDiffModel GenerateSideBySideDiff(string originalContent, string modifiedContent)
    {
        return _sideBySideDiffBuilder.BuildDiffModel(originalContent, modifiedContent);
    }
    
    /// <summary>
    /// Creates a preview of what changes would be made by an edit operation
    /// </summary>
    public string CreateEditPreview(string[] originalLines, EditOperation operation)
    {
        var modifiedLines = ApplyOperationToLines(originalLines, operation);
        var originalContent = string.Join('\n', originalLines);
        var modifiedContent = string.Join('\n', modifiedLines);
        
        return GenerateUnifiedDiff(originalContent, modifiedContent);
    }
    
    /// <summary>
    /// Validates that an edit operation would produce valid results
    /// </summary>
    public static (bool isValid, string? errorMessage) ValidateEdit(string[] originalLines, EditOperation operation)
    {
        try
        {
            // Check line range bounds
            if (operation.StartLine < 1 || operation.StartLine > originalLines.Length + 1)
                return (false, $"Start line {operation.StartLine} is out of bounds (file has {originalLines.Length} lines)");
            
            if (operation.EndLine < operation.StartLine || operation.EndLine > originalLines.Length + 1)
                return (false, $"End line {operation.EndLine} is invalid (start: {operation.StartLine}, file has {originalLines.Length} lines)");

            // Try to apply the operation
            ApplyOperationToLines(originalLines, operation);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
    
    private static string[] ApplyOperationToLines(string[] originalLines, EditOperation operation)
    {
        List<string> result = [..originalLines];
        
        switch (operation.Type)
        {
            case EditOperationType.Replace:
                // Remove the old lines and insert new ones
                var removeCount = operation.EndLine - operation.StartLine + 1;
                result.RemoveRange(operation.StartLine - 1, removeCount);
                
                if (!string.IsNullOrEmpty(operation.Content))
                {
                    var newLines = SplitLines(operation.Content);
                    result.InsertRange(operation.StartLine - 1, newLines);
                }
                break;
                
            case EditOperationType.Insert:
                if (!string.IsNullOrEmpty(operation.Content))
                {
                    var newLines = SplitLines(operation.Content);
                    result.InsertRange(operation.StartLine, newLines); // Insert after the line
                }
                break;
                
            case EditOperationType.Delete:
                var deleteCount = operation.EndLine - operation.StartLine + 1;
                result.RemoveRange(operation.StartLine - 1, deleteCount);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unknown operation type: {operation.Type}");
        }
        
        return result.ToArray();
    }
    
    private static string[] SplitLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return [];
            
        return content.Split(['\n', '\r'], StringSplitOptions.None)
                     .Where(line => !line.Equals("\r"))
                     .ToArray();
    }
    
    private static string FormatAsUnifiedDiff(DiffResult diff, string filePath, string[] originalLines, string[] modifiedLines)
    {
        var result = new List<string>();
        
        if (!string.IsNullOrEmpty(filePath))
        {
            result.Add($"--- {filePath} (original)");
            result.Add($"+++ {filePath} (modified)");
        }
        
        foreach (var block in diff.DiffBlocks)
        {
            var headerOldStart = block.DeleteStartA + 1;
            var headerOldCount = block.DeleteCountA;
            var headerNewStart = block.InsertStartB + 1;
            var headerNewCount = block.InsertCountB;
            
            result.Add($"@@ -{headerOldStart},{headerOldCount} +{headerNewStart},{headerNewCount} @@");
            
            // Add deleted lines
            for (var j = 0; j < block.DeleteCountA; j++)
            {
                var lineIndex = block.DeleteStartA + j;
                if (lineIndex < originalLines.Length)
                    result.Add($"-{originalLines[lineIndex]}");
            }
            
            // Add inserted lines
            for (var j = 0; j < block.InsertCountB; j++)
            {
                var lineIndex = block.InsertStartB + j;
                if (lineIndex < modifiedLines.Length)
                    result.Add($"+{modifiedLines[lineIndex]}");
            }
        }
        
        return string.Join('\n', result);
    }
}