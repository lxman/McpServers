using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Commands;

/// <summary>
/// Base interface for all refactoring commands using the Command Pattern.
/// Provides a unified interface for executing different refactoring operations
/// with support for preview mode and cancellation.
/// </summary>
public interface IRefactoringCommand
{
    /// <summary>
    /// Gets the unique identifier for this command type.
    /// </summary>
    string CommandId { get; }
    
    /// <summary>
    /// Gets a human-readable description of what this command does.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Gets the supported languages for this command.
    /// </summary>
    IEnumerable<LanguageType> SupportedLanguages { get; }
    
    /// <summary>
    /// Determines if this command supports the specified file type.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the command supports this file type</returns>
    bool SupportsFile(string filePath);
    
    /// <summary>
    /// Validates the command parameters before execution.
    /// </summary>
    /// <param name="context">The refactoring context containing parameters</param>
    /// <returns>Validation result with any error messages</returns>
    Task<RefactoringResult> ValidateAsync(RefactoringContext context);
    
    /// <summary>
    /// Executes the refactoring command.
    /// </summary>
    /// <param name="context">The refactoring context containing all necessary parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the refactoring operation</returns>
    Task<RefactoringResult> ExecuteAsync(RefactoringContext context, CancellationToken cancellationToken = default);
}