using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Base interface for all refactoring operations
/// </summary>
public interface IRefactoringOperation
{
    /// <summary>
    /// Gets the name of the refactoring operation
    /// </summary>
    string OperationName { get; }
    
    /// <summary>
    /// Gets the supported language type for this operation
    /// </summary>
    LanguageType SupportedLanguage { get; }
    
    /// <summary>
    /// Validates that the operation can be performed with the given context
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <returns>True if the operation is valid, false otherwise</returns>
    Task<bool> ValidateOperationAsync(RefactoringContext context);
    
    /// <summary>
    /// Executes the refactoring operation
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <returns>The result of the refactoring operation</returns>
    Task<RefactoringResult> ExecuteAsync(RefactoringContext context);
}
