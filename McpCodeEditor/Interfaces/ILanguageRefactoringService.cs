using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Language-specific refactoring service interface
/// </summary>
public interface ILanguageRefactoringService
{
    /// <summary>
    /// Gets the language type this service supports
    /// </summary>
    LanguageType SupportedLanguage { get; }
    
    /// <summary>
    /// Extracts a method from the specified code lines
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <param name="methodName">Name for the extracted method</param>
    /// <param name="startLine">Starting line number (1-based)</param>
    /// <param name="endLine">Ending line number (1-based)</param>
    /// <param name="previewOnly">If true, returns preview without modifying files</param>
    /// <returns>The refactoring result</returns>
    Task<RefactoringResult> ExtractMethodAsync(RefactoringContext context, string methodName, int startLine, int endLine, bool previewOnly = false);
    
    /// <summary>
    /// Inlines a method by replacing all call sites with the method body
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <param name="methodName">Name of the method to inline</param>
    /// <param name="previewOnly">If true, returns preview without modifying files</param>
    /// <returns>The refactoring result</returns>
    Task<RefactoringResult> InlineMethodAsync(RefactoringContext context, string methodName, bool previewOnly = false);
    
    /// <summary>
    /// Introduces a local variable for the specified expression
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <param name="line">Line number containing the expression (1-based)</param>
    /// <param name="startColumn">Starting column of the expression (1-based)</param>
    /// <param name="endColumn">Ending column of the expression (1-based)</param>
    /// <param name="variableName">Optional name for the new variable</param>
    /// <param name="previewOnly">If true, returns preview without modifying files</param>
    /// <returns>The refactoring result</returns>
    Task<RefactoringResult> IntroduceVariableAsync(RefactoringContext context, int line, int startColumn, int endColumn, string? variableName = null, bool previewOnly = false);
    
    /// <summary>
    /// Organizes and sorts import/using statements
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <param name="removeUnused">Remove unused imports</param>
    /// <param name="sortAlphabetically">Sort imports alphabetically</param>
    /// <param name="previewOnly">If true, returns preview without modifying files</param>
    /// <returns>The refactoring result</returns>
    Task<RefactoringResult> OrganizeImportsAsync(RefactoringContext context, bool removeUnused = true, bool sortAlphabetically = true, bool previewOnly = false);
    
    /// <summary>
    /// Adds an import/using statement
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <param name="importStatement">The import statement to add</param>
    /// <param name="previewOnly">If true, returns preview without modifying files</param>
    /// <returns>The refactoring result</returns>
    Task<RefactoringResult> AddImportAsync(RefactoringContext context, string importStatement, bool previewOnly = false);
}
