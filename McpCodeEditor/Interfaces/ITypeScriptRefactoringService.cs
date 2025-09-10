using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for TypeScript refactoring operations
/// Provides abstraction for all TypeScript-specific refactoring capabilities
/// </summary>
public interface ITypeScriptRefactoringService
{
    /// <summary>
    /// Extract a TypeScript method from selected lines with comprehensive validation
    /// </summary>
    /// <param name="filePath">Path to the TypeScript file</param>
    /// <param name="options">Method extraction options</param>
    /// <param name="previewOnly">If true, only shows what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refactoring the result with extracted method information</returns>
    Task<TypeScriptMethodExtractor.TypeScriptExtractionResult> ExtractMethodAsync(
        string filePath,
        TypeScriptExtractionOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Introduce a TypeScript variable from selected expression
    /// </summary>
    /// <param name="filePath">Path to the TypeScript file</param>
    /// <param name="line">Line number containing the expression</param>
    /// <param name="startColumn">Starting column of the expression</param>
    /// <param name="endColumn">Ending column of the expression</param>
    /// <param name="variableName">Optional variable name (auto-generated if not provided)</param>
    /// <param name="declarationType">Variable declaration type: 'const', 'let', 'var'</param>
    /// <param name="previewOnly">If true, only shows what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refactoring the result with variable introduction information</returns>
    Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName = null,
        string declarationType = "const",
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inline a TypeScript function by replacing all call sites with function body
    /// </summary>
    /// <param name="filePath">Path to the TypeScript file</param>
    /// <param name="functionName">Name of the function to inline</param>
    /// <param name="inlineScope">Inline scope: 'file' (current file only) or 'project' (all files)</param>
    /// <param name="previewOnly">If true, only shows what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refactoring the result with inlining information</returns>
    Task<RefactoringResult> InlineFunctionAsync(
        string filePath,
        string functionName,
        string inlineScope = "file",
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Organize and sort TypeScript import statements
    /// </summary>
    /// <param name="filePath">Path to the TypeScript file</param>
    /// <param name="sortAlphabetically">Whether to sort imports alphabetically</param>
    /// <param name="groupByType">Whether to group imports by type</param>
    /// <param name="removeUnused">Whether to remove unused imports (basic detection)</param>
    /// <param name="previewOnly">If true, only shows what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refactoring the result with import organization information</returns>
    Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool sortAlphabetically = true,
        bool groupByType = true,
        bool removeUnused = false,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add an import statement to a TypeScript file
    /// </summary>
    /// <param name="filePath">Path to the TypeScript file</param>
    /// <param name="importStatement">Import statement to add</param>
    /// <param name="previewOnly">If true, only shows what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refactoring the result with import addition information</returns>
    Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);
}
