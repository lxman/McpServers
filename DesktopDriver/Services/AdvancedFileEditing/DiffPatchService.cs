﻿using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DesktopDriver.Services.AdvancedFileEditing.Models;
using DiffPlex.Model;

namespace DesktopDriver.Services.AdvancedFileEditing;

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
        DiffResult? diff = _differ.CreateDiffs(originalContent, modifiedContent, true, false, DiffPlex.Chunkers.LineChunker.Instance);
        string[] originalLines = SplitLines(originalContent);
        string[] modifiedLines = SplitLines(modifiedContent);
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
        string[] modifiedLines = ApplyOperationToLines(originalLines, operation);
        string originalContent = string.Join('\n', originalLines);
        string modifiedContent = string.Join('\n', modifiedLines);
        
        return GenerateUnifiedDiff(originalContent, modifiedContent);
    }
    
    /// <summary>
    /// Validates that an edit operation would produce valid results
    /// </summary>
    public (bool isValid, string? errorMessage) ValidateEdit(string[] originalLines, EditOperation operation)
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
    
    private string[] ApplyOperationToLines(string[] originalLines, EditOperation operation)
    {
        var result = new List<string>(originalLines);
        
        switch (operation.Type)
        {
            case EditOperationType.Replace:
                // Remove the old lines and insert new ones
                int removeCount = operation.EndLine - operation.StartLine + 1;
                result.RemoveRange(operation.StartLine - 1, removeCount);
                
                if (!string.IsNullOrEmpty(operation.Content))
                {
                    string[] newLines = SplitLines(operation.Content);
                    result.InsertRange(operation.StartLine - 1, newLines);
                }
                break;
                
            case EditOperationType.Insert:
                if (!string.IsNullOrEmpty(operation.Content))
                {
                    string[] newLines = SplitLines(operation.Content);
                    result.InsertRange(operation.StartLine, newLines); // Insert after the line
                }
                break;
                
            case EditOperationType.Delete:
                int deleteCount = operation.EndLine - operation.StartLine + 1;
                result.RemoveRange(operation.StartLine - 1, deleteCount);
                break;
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
        
        foreach (DiffBlock? block in diff.DiffBlocks)
        {
            int headerOldStart = block.DeleteStartA + 1;
            int headerOldCount = block.DeleteCountA;
            int headerNewStart = block.InsertStartB + 1;
            int headerNewCount = block.InsertCountB;
            
            result.Add($"@@ -{headerOldStart},{headerOldCount} +{headerNewStart},{headerNewCount} @@");
            
            // Add deleted lines
            for (var j = 0; j < block.DeleteCountA; j++)
            {
                int lineIndex = block.DeleteStartA + j;
                if (lineIndex < originalLines.Length)
                    result.Add($"-{originalLines[lineIndex]}");
            }
            
            // Add inserted lines
            for (var j = 0; j < block.InsertCountB; j++)
            {
                int lineIndex = block.InsertStartB + j;
                if (lineIndex < modifiedLines.Length)
                    result.Add($"+{modifiedLines[lineIndex]}");
            }
        }
        
        return string.Join('\n', result);
    }
}