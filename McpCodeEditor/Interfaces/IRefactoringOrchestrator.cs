using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Main orchestrator interface for coordinating refactoring operations across different languages.
/// Implements the Open/Closed Principle by delegating to language-specific services.
/// </summary>
public interface IRefactoringOrchestrator
{
    /// <summary>
    /// Extracts a method from the specified code range in a file.
    /// Delegates to appropriate language-specific service based on file type.
    /// </summary>
    /// <param name="filePath">Path to the file containing the code to extract</param>
    /// <param name="methodName">Name for the new method</param>
    /// <param name="startLine">Starting line number (1-based)</param>
    /// <param name="endLine">Ending line number (1-based)</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the extracted method and any changes made</returns>
    Task<RefactoringResult> ExtractMethodAsync(
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a method from the specified code range in a file with advanced options.
    /// Delegates to appropriate language-specific service based on file type.
    /// </summary>
    /// <param name="filePath">Path to the file containing the code to extract</param>
    /// <param name="methodName">Name for the new method</param>
    /// <param name="startLine">Starting line number (1-based)</param>
    /// <param name="endLine">Ending line number (1-based)</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="accessModifier">Access modifier for the new method</param>
    /// <param name="isStatic">Whether the method should be static</param>
    /// <param name="returnType">Return type for the new method (auto-detected if not specified)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the extracted method and any changes made</returns>
    Task<RefactoringResult> ExtractMethodAsync(
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly,
        string accessModifier,
        bool isStatic,
        string? returnType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inlines a method by replacing all call sites with the method body.
    /// Delegates to appropriate language-specific service based on file type.
    /// </summary>
    /// <param name="filePath">Path to the file containing the method to inline</param>
    /// <param name="methodName">Name of the method to inline</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the inlined code and any changes made</returns>
    Task<RefactoringResult> InlineMethodAsync(
        string filePath,
        string methodName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Introduces a variable for the specified expression.
    /// Delegates to appropriate language-specific service based on file type.
    /// </summary>
    /// <param name="filePath">Path to the file containing the expression</param>
    /// <param name="line">Line number containing the expression (1-based)</param>
    /// <param name="startColumn">Starting column of the expression (1-based)</param>
    /// <param name="endColumn">Ending column of the expression (1-based)</param>
    /// <param name="variableName">Optional name for the new variable</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the new variable declaration and usage</returns>
    Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Organizes import/using statements in the specified file.
    /// Delegates to appropriate language-specific service based on file type.
    /// </summary>
    /// <param name="filePath">Path to the file to organize imports for</param>
    /// <param name="removeUnused">Whether to remove unused imports</param>
    /// <param name="sortAlphabetically">Whether to sort imports alphabetically</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the organized imports</returns>
    Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool removeUnused = true,
        bool sortAlphabetically = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an import/using statement to the specified file.
    /// Delegates to appropriate language-specific service based on file type.
    /// </summary>
    /// <param name="filePath">Path to the file to add import to</param>
    /// <param name="importStatement">Import statement to add</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the added import statement</returns>
    Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encapsulates a field by converting it to a property.
    /// Currently only supported for C# files.
    /// </summary>
    /// <param name="filePath">Path to the C# file</param>
    /// <param name="fieldName">Name of the field to encapsulate</param>
    /// <param name="propertyName">Optional name for the new property</param>
    /// <param name="useAutoProperty">Whether to use auto-property syntax</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the encapsulated field</returns>
    Task<RefactoringResult> EncapsulateFieldAsync(
        string filePath,
        string fieldName,
        string? propertyName = null,
        bool useAutoProperty = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a symbol throughout the project or file scope.
    /// Delegates to appropriate language-specific service based on file type.
    /// </summary>
    /// <param name="filePath">Optional file path to limit scope</param>
    /// <param name="symbolName">Current symbol name to rename</param>
    /// <param name="newName">New name for the symbol</param>
    /// <param name="previewOnly">If true, only show what would be changed without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing all renamed symbol references</returns>
    Task<RefactoringResult> RenameSymbolAsync(
        string? filePath,
        string symbolName,
        string newName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the supported languages for refactoring operations.
    /// </summary>
    /// <returns>List of supported language types</returns>
    IEnumerable<LanguageType> GetSupportedLanguages();

    /// <summary>
    /// Checks if a specific refactoring operation is supported for the given file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="operationType">Type of refactoring operation</param>
    /// <returns>True if the operation is supported for this file type</returns>
    bool IsOperationSupported(string filePath, string operationType);
}
