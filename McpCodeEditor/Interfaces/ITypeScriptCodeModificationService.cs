using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for TypeScript code modification operations
/// Phase A2-T4: Extracted from TypeScriptVariableOperations for focused code modification logic
/// Handles scope-aware code insertion, indentation, and modification operations
/// </summary>
public interface ITypeScriptCodeModificationService
{
    /// <summary>
    /// Apply a scope-aware variable introduction with proper placement and indentation
    /// </summary>
    /// <param name="lines">Original file lines</param>
    /// <param name="targetLine">Target line number (1-based)</param>
    /// <param name="startColumn">Start column of expression (1-based)</param>
    /// <param name="endColumn">End column of expression (1-based)</param>
    /// <param name="variableName">Name of variable to introduce</param>
    /// <param name="selectedExpression">Selected expression text</param>
    /// <param name="declarationResult">Variable declaration result</param>
    /// <param name="scopeAnalysis">Scope analysis result</param>
    /// <returns>Variable modification result with updated lines</returns>
    VariableModificationResult ApplyScopeAwareVariableIntroduction(
        string[] lines,
        int targetLine,
        int startColumn,
        int endColumn,
        string variableName,
        string selectedExpression,
        VariableDeclarationResult declarationResult,
        TypeScriptScopeAnalysisResult scopeAnalysis);

    /// <summary>
    /// Find the appropriate insertion point for variable declaration based on scope
    /// </summary>
    /// <param name="lines">File lines</param>
    /// <param name="targetLine">Target line number (1-based)</param>
    /// <param name="scopeAnalysis">Scope analysis result</param>
    /// <returns>Insertion line index (0-based)</returns>
    int FindScopeAwareInsertionPoint(string[] lines, int targetLine, TypeScriptScopeAnalysisResult scopeAnalysis);

    /// <summary>
    /// Get appropriate indentation based on scope context and surrounding code
    /// </summary>
    /// <param name="lines">File lines</param>
    /// <param name="insertionLine">Insertion line index (0-based)</param>
    /// <param name="scopeAnalysis">Scope analysis result</param>
    /// <returns>Indentation string</returns>
    string GetScopeAwareIndentation(string[] lines, int insertionLine, TypeScriptScopeAnalysisResult scopeAnalysis);
}
