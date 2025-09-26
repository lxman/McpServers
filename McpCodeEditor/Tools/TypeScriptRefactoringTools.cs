using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring.TypeScript;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools;

/// <summary>
/// MCP tools for TypeScript and JavaScript refactoring operations.
/// </summary>
public class TypeScriptRefactoringTools(
    IRefactoringOrchestrator refactoringOrchestrator,
    TypeScriptCrossFileRenamer crossFileRenamer) : BaseToolClass
{
    #region TypeScript Import Organization

    [McpServerTool]
    [Description("Organize and sort TypeScript/JavaScript import statements")]
    public async Task<string> RefactorOrganizeTypeScriptImportsAsync(
        [Description("Path to the TypeScript/JavaScript file (.ts, .tsx, .js, .jsx)")]
        string filePath,
        [Description("Remove unused import statements (basic detection)")]
        bool removeUnused = true,
        [Description("Sort import statements alphabetically")]
        bool sortAlphabetically = true,
        [Description("Group imports by type (libraries, relative paths, etc.)")]
        bool groupByType = true,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);

            RefactoringResult result = await refactoringOrchestrator.OrganizeImportsAsync(
                filePath, removeUnused, sortAlphabetically, previewOnly);
            return result;
        });
    }

    [McpServerTool]
    [Description("Add an import statement to a TypeScript/JavaScript file")]
    public async Task<string> RefactorAddTypeScriptImportAsync(
        [Description("Path to the TypeScript/JavaScript file (.ts, .tsx, .js, .jsx)")]
        string filePath,
        [Description("Import statement to add (e.g., \"import { Component } from 'react'\")")]
        string importStatement,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateRequiredParameter(importStatement, nameof(importStatement));

            RefactoringResult result = await refactoringOrchestrator.AddImportAsync(
                filePath, importStatement, previewOnly);
            return result;
        });
    }

    #endregion

    #region TypeScript Refactoring Operations

    [McpServerTool]
    [Description("Extract a function from selected lines of TypeScript/JavaScript code")]
    public async Task<string> RefactorExtractTypeScriptMethodAsync(
        [Description("Path to the TypeScript/JavaScript file (.ts, .tsx, .js, .jsx)")]
        string filePath,
        [Description("Name for the new function")]
        string functionName,
        [Description("Starting line number (1-based)")]
        int startLine,
        [Description("Ending line number (1-based)")]
        int endLine,
        [Description("Function type: 'function', 'arrow', 'async', 'async-arrow'")]
        string functionType = "function",
        [Description("Export the function: 'none', 'export', 'export-default'")]
        string exportType = "none",
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateRequiredParameter(functionName, nameof(functionName));
            ValidateLineNumber(startLine, nameof(startLine));
            ValidateLineNumber(endLine, nameof(endLine));

            if (endLine < startLine)
            {
                throw new ArgumentException("End line must be greater than or equal to start line.");
            }

            // Use the new orchestrator for TypeScript method extraction
            RefactoringResult result = await refactoringOrchestrator.ExtractMethodAsync(filePath, functionName, startLine, endLine, previewOnly);
            return result;
        });
    }

    [McpServerTool]
    [Description("Extract a selected expression into a TypeScript/JavaScript variable")]
    public async Task<string> RefactorIntroduceTypeScriptVariableAsync(
        [Description("Path to the TypeScript/JavaScript file (.ts, .tsx, .js, .jsx)")]
        string filePath,
        [Description("Line number containing the expression (1-based)")]
        int line,
        [Description("Starting column of the expression (1-based)")]
        int startColumn,
        [Description("Ending column of the expression (1-based)")]
        int endColumn,
        [Description("Optional name for the new variable (auto-generated if not provided)")]
        string? variableName = null,
        [Description("Variable declaration type: 'const', 'let', 'var'")]
        string declarationType = "const",
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateLineNumber(line);

            if (startColumn <= 0)
            {
                throw new ArgumentException("Start column must be positive (1-based).", nameof(startColumn));
            }

            if (endColumn <= 0)
            {
                throw new ArgumentException("End column must be positive (1-based).", nameof(endColumn));
            }

            if (endColumn <= startColumn)
            {
                throw new ArgumentException("End column must be greater than start column.");
            }

            // Use the new orchestrator for TypeScript variable introduction
            RefactoringResult result = await refactoringOrchestrator.IntroduceVariableAsync(
                filePath, line, startColumn, endColumn, variableName, previewOnly);
            return result;
        });
    }

    [McpServerTool]
    [Description("Inline a TypeScript/JavaScript function by replacing all call sites with the function body")]
    public async Task<string> RefactorInlineTypeScriptFunctionAsync(
        [Description("Path to the TypeScript/JavaScript file (.ts, .tsx, .js, .jsx)")]
        string filePath,
        [Description("Name of the function to inline")]
        string functionName,
        [Description("Inline scope: 'file' (current file only) or 'project' (all files)")]
        string inlineScope = "file",
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateRequiredParameter(functionName, nameof(functionName));

            // Use the new orchestrator for TypeScript function inlining
            RefactoringResult result = await refactoringOrchestrator.InlineMethodAsync(
                filePath, functionName, previewOnly);
            return result;
        });
    }

    [McpServerTool]
    [Description("Rename a TypeScript symbol across files with intelligent pattern matching")]
    public async Task<string> TypeScriptRenameSymbolAsync(
        [Description("Current symbol name to rename")]
        string symbolName,
        [Description("New name for the symbol")]
        string newName,
        [Description("Optional file path to limit search scope")]
        string? filePath = null,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(symbolName, nameof(symbolName));
            ValidateRequiredParameter(newName, nameof(newName));

            if (!string.IsNullOrEmpty(filePath))
            {
                ValidateFilePath(filePath);
            }

            // Validate symbol names
            if (symbolName.Trim().Length == 0)
            {
                throw new ArgumentException("Symbol name cannot be empty.", nameof(symbolName));
            }

            if (newName.Trim().Length == 0)
            {
                throw new ArgumentException("New name cannot be empty.", nameof(newName));
            }

            if (symbolName == newName)
            {
                throw new ArgumentException("New name must be different from the current symbol name.");
            }

            // FIXED: Call orchestrator with correct parameter order (filePath, symbolName, newName, previewOnly)
            RefactoringResult result = await refactoringOrchestrator.RenameSymbolAsync(filePath, symbolName, newName, previewOnly);
            return result;
        });
    }

    [McpServerTool]
    [Description("?? CONVERSATION-SAFE: Advanced cross-file TypeScript symbol renaming with dependency analysis (uses summary mode by default to prevent large responses)")]
    public async Task<string> TypeScriptCrossFileRenameSymbolAsync(
        [Description("Current symbol name to rename")]
        string symbolName,
        [Description("New name for the symbol")]
        string newName,
        [Description("Optional project root path (defaults to current workspace)")]
        string? rootPath = null,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false,
        [Description("Use summary mode (recommended) - set to false for full details but may return 100k+ characters and terminate conversation")]
        bool useSummaryMode = true)
    {
        return await ExecuteWithErrorHandlingAsync<SummaryRefactoringResult>(async () =>
        {
            ValidateRequiredParameter(symbolName, nameof(symbolName));
            ValidateRequiredParameter(newName, nameof(newName));

            // Validate symbol names
            if (symbolName.Trim().Length == 0)
            {
                throw new ArgumentException("Symbol name cannot be empty.", nameof(symbolName));
            }

            if (newName.Trim().Length == 0)
            {
                throw new ArgumentException("New name cannot be empty.", nameof(newName));
            }

            if (symbolName == newName)
            {
                throw new ArgumentException("New name must be different from the current symbol name.");
            }

            // Use the unified method that always returns SummaryRefactoringResult
            SummaryRefactoringResult result = await crossFileRenamer.RenameSymbolAcrossFilesUnifiedAsync(
                symbolName, newName, rootPath, previewOnly, useSummaryMode);
            
            return result;
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Maps a string function type to TypeScript function type enum
    /// </summary>
    /// <param name="functionType">String representation of a function type</param>
    /// <returns>TypeScriptFunctionType enum value</returns>
    private static TypeScriptFunctionType MapStringToFunctionType(string functionType)
    {
        return functionType.ToLowerInvariant() switch
        {
            "function" => TypeScriptFunctionType.Function,
            "arrow" => TypeScriptFunctionType.ArrowFunction,
            "async" => TypeScriptFunctionType.AsyncFunction,
            "async-arrow" => TypeScriptFunctionType.AsyncArrowFunction,
            "method" => TypeScriptFunctionType.Method,
            _ => TypeScriptFunctionType.Function // Default fallback
        };
    }

    #endregion
}
