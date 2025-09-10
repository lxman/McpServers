using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Strategies;

/// <summary>
/// Strategy interface for language-specific refactoring operations.
/// Implements the Strategy pattern to eliminate language-switching logic from RefactoringOrchestrator.
/// </summary>
public interface ILanguageRefactoringStrategy
{
    /// <summary>
    /// The language type this strategy handles
    /// </summary>
    LanguageType Language { get; }

    /// <summary>
    /// Extract a method from the specified lines of code
    /// </summary>
    Task<RefactoringResult> ExtractMethodAsync(
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract a method with advanced options (access modifier, static, return type)
    /// </summary>
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
    /// Inline a method by replacing all call sites with the method body
    /// </summary>
    Task<RefactoringResult> InlineMethodAsync(
        string filePath,
        string methodName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract an expression into a variable
    /// </summary>
    Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Organize import/using statements
    /// </summary>
    Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool removeUnused = true,
        bool sortAlphabetically = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add an import/using statement
    /// </summary>
    Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encapsulate a field as a property (C# specific, may not be supported by all languages)
    /// </summary>
    Task<RefactoringResult> EncapsulateFieldAsync(
        string filePath,
        string fieldName,
        string? propertyName = null,
        bool useAutoProperty = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rename a symbol across the file or project
    /// </summary>
    Task<RefactoringResult> RenameSymbolAsync(
        string? filePath,
        string symbolName,
        string newName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific operation is supported by this language strategy
    /// </summary>
    bool IsOperationSupported(string operationType);
}
