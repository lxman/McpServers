using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.TypeScript;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Services.TypeScript;

/// <summary>
/// Service for TypeScript code modification operations
/// Phase A2-T4: Extracted from TypeScriptVariableOperations for focused code modification logic
/// Handles scope-aware code insertion, indentation, and modification operations
/// </summary>
public class TypeScriptCodeModificationService(ILogger<TypeScriptCodeModificationService> logger) : ITypeScriptCodeModificationService
{
    /// <summary>
    /// Apply a scope-aware variable introduction with proper placement and indentation
    /// Phase A2-T4: Extracted from TypeScriptVariableOperations.ApplyScopeAwareVariableIntroduction
    /// </summary>
    public VariableModificationResult ApplyScopeAwareVariableIntroduction(
        string[] lines,
        int targetLine,
        int startColumn,
        int endColumn,
        string variableName,
        string selectedExpression,
        VariableDeclarationResult declarationResult,
        TypeScriptScopeAnalysisResult scopeAnalysis)
    {
        try
        {
            logger.LogDebug("Applying scope-aware variable introduction for {VariableName} at line {TargetLine}", variableName, targetLine);

            var modifiedLines = new List<string>(lines);
            TypeScriptVariablePlacementStrategy strategy = declarationResult.PlacementStrategy;

            // Find the appropriate insertion point based on scope
            int insertionLine = FindScopeAwareInsertionPoint(lines, targetLine, scopeAnalysis);
            
            logger.LogDebug("TS-013 REF-002: Inserting at line {InsertionLine} for {PlacementLocation}",
                insertionLine, strategy.PlacementLocation);

            // Insert variable declaration with appropriate indentation
            string indentation = declarationResult.RequiresIndentation ?
                GetScopeAwareIndentation(lines, insertionLine, scopeAnalysis) : "";
            string fullDeclaration = indentation + declarationResult.Declaration;

            modifiedLines.Insert(insertionLine, fullDeclaration);

            // Replace the original expression with variable reference
            string targetLineContent = lines[targetLine - 1];
            string variableReference = strategy.RequiresThisPrefix ? $"this.{variableName}" : variableName;
            
            string updatedLine = targetLineContent[..(startColumn - 1)] + variableReference + 
                               targetLineContent[endColumn..];
            
            // Adjust line index due to insertion
            int updatedLineIndex = targetLine;
            if (insertionLine <= targetLine - 1)
            {
                updatedLineIndex++;
            }
            
            modifiedLines[updatedLineIndex - 1] = updatedLine;

            logger.LogDebug("Successfully applied variable introduction: {VariableName}", variableName);

            return new VariableModificationResult
            {
                Success = true,
                ModifiedLines = modifiedLines,
                InsertionLine = insertionLine,
                UpdatedLine = updatedLineIndex,
                VariableReference = variableReference
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply scope-aware variable introduction for {VariableName}", variableName);
            return new VariableModificationResult
            {
                Success = false,
                ErrorMessage = $"Modification failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Find the appropriate insertion point for variable declaration based on scope
    /// Phase A2-T4: Extracted from TypeScriptVariableOperations.FindScopeAwareInsertionPoint
    /// </summary>
    public int FindScopeAwareInsertionPoint(string[] lines, int targetLine, TypeScriptScopeAnalysisResult scopeAnalysis)
    {
        try
        {
            logger.LogDebug("Finding scope-aware insertion point for target line {TargetLine}", targetLine);

            TypeScriptVariablePlacementStrategy strategy = scopeAnalysis.VariablePlacementStrategy;

            switch (strategy.PlacementLocation)
            {
                case VariablePlacementLocation.ClassMember:
                    // Insert as class member - find a good spot in the class
                    TypeScriptScopeInfo? classScope = scopeAnalysis.ScopeHierarchy.FirstOrDefault(s => s.ScopeType == TypeScriptScopeType.Class);
                    if (classScope != null)
                    {
                        // Find the end of existing properties/fields
                        for (int i = classScope.StartLine; i < Math.Min(targetLine, lines.Length); i++)
                        {
                            string line = lines[i - 1].Trim();
                            // Insert before the first method or at the end of properties
                            if (line.Contains("(") && (line.Contains("constructor") || 
                                Regex.IsMatch(line, @"\w+\s*\([^)]*\)\s*(\:\s*\w+)?\s*\{")))
                            {
                                logger.LogDebug("Found class member insertion point at line {InsertionLine}", i - 1);
                                return i - 1;
                            }
                        }
                        // Fallback: insert after class declaration
                        logger.LogDebug("Using fallback class member insertion point at line {InsertionLine}", classScope.StartLine);
                        return classScope.StartLine;
                    }
                    break;

                case VariablePlacementLocation.MethodLocal:
                case VariablePlacementLocation.FunctionLocal:
                    // Insert at the beginning of the method/function
                    TypeScriptScopeInfo? methodScope = scopeAnalysis.ScopeHierarchy.LastOrDefault(s => 
                        s.ScopeType is TypeScriptScopeType.Method or TypeScriptScopeType.Constructor or TypeScriptScopeType.Function);
                    if (methodScope != null)
                    {
                        logger.LogDebug("Found method/function insertion point at line {InsertionLine}", methodScope.StartLine);
                        return methodScope.StartLine;
                    }
                    break;

                case VariablePlacementLocation.BlockLocal:
                    // Insert at the beginning of the current block
                    TypeScriptScopeInfo? blockScope = scopeAnalysis.ScopeHierarchy.LastOrDefault(s => s.ScopeType == TypeScriptScopeType.Block);
                    if (blockScope != null)
                    {
                        logger.LogDebug("Found block insertion point at line {InsertionLine}", blockScope.StartLine);
                        return blockScope.StartLine;
                    }
                    break;

                case VariablePlacementLocation.ModuleLevel:
                    // Insert at the top of the file (after imports)
                    for (var i = 0; i < Math.Min(targetLine, lines.Length); i++)
                    {
                        string line = lines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(line) && 
                            !line.StartsWith("import") && 
                            !line.StartsWith("//") && 
                            !line.StartsWith("/*"))
                        {
                            logger.LogDebug("Found module-level insertion point at line {InsertionLine}", i);
                            return i;
                        }
                    }
                    break;
            }

            // Fallback: insert before the current line
            int fallbackLine = Math.Max(0, targetLine - 1);
            logger.LogDebug("Using fallback insertion point at line {InsertionLine}", fallbackLine);
            return fallbackLine;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding scope-aware insertion point");
            return Math.Max(0, targetLine - 1);
        }
    }

    /// <summary>
    /// Get appropriate indentation based on scope context and surrounding code
    /// Phase A2-T4: Extracted from TypeScriptVariableOperations.GetScopeAwareIndentation
    /// </summary>
    public string GetScopeAwareIndentation(string[] lines, int insertionLine, TypeScriptScopeAnalysisResult scopeAnalysis)
    {
        try
        {
            logger.LogDebug("Determining scope-aware indentation for insertion line {InsertionLine}", insertionLine);

            TypeScriptVariablePlacementStrategy strategy = scopeAnalysis.VariablePlacementStrategy;

            // For class members, use class-level indentation
            if (strategy.PlacementLocation == VariablePlacementLocation.ClassMember)
            {
                TypeScriptScopeInfo? classScope = scopeAnalysis.ScopeHierarchy.FirstOrDefault(s => s.ScopeType == TypeScriptScopeType.Class);
                if (classScope != null && classScope.StartLine <= lines.Length)
                {
                    string classLine = lines[classScope.StartLine - 1];
                    string classIndentation = GetIndentation(classLine) + "    "; // Add one level of indentation
                    logger.LogDebug("Using class member indentation: '{Indentation}'", classIndentation);
                    return classIndentation;
                }
            }

            // For method/function locals, match the surrounding code indentation
            if (insertionLine > 0 && insertionLine <= lines.Length)
            {
                // Look for a nearby non-empty line to match indentation
                for (int i = Math.Max(0, insertionLine - 1); i < Math.Min(insertionLine + 3, lines.Length); i++)
                {
                    string line = lines[i];
                    if (!string.IsNullOrWhiteSpace(line.Trim()))
                    {
                        string indentation = GetIndentation(line);
                        logger.LogDebug("Using surrounding code indentation: '{Indentation}'", indentation);
                        return indentation;
                    }
                }
            }

            // Fallback: no indentation
            logger.LogDebug("Using fallback indentation (empty)");
            return "";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error determining scope-aware indentation");
            return "";
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Extract indentation from a line of code
    /// </summary>
    private static string GetIndentation(string line)
    {
        var indentCount = 0;
        foreach (char c in line)
        {
            if (c == ' ')
                indentCount++;
            else if (c == '\t')
                indentCount += 4; // Assume 4 spaces per tab
            else
                break;
        }
        return new string(' ', indentCount);
    }

    #endregion
}
