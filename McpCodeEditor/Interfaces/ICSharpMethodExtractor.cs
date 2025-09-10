using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for C# method extraction operations
/// </summary>
public interface ICSharpMethodExtractor : IRefactoringOperation
{
    /// <summary>
    /// Extracts a method from selected lines of C# code with comprehensive validation
    /// </summary>
    /// <param name="context">The refactoring context containing file and operation details</param>
    /// <param name="options">C# specific extraction options</param>
    /// <param name="previewOnly">Whether to only preview the changes without applying them</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the method extraction operation</returns>
    Task<RefactoringResult> ExtractMethodAsync(
        RefactoringContext context,
        CSharpExtractionOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that method extraction can be performed with the given options
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <param name="options">C# specific extraction options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any errors or warnings</returns>
    Task<CSharpValidationResult> ValidateExtractionAsync(
        RefactoringContext context,
        CSharpExtractionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes the code to be extracted and provides insights
    /// </summary>
    /// <param name="context">The refactoring context</param>
    /// <param name="options">C# specific extraction options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis result with complexity metrics and suggestions</returns>
    Task<CSharpExtractionAnalysis> AnalyzeExtractionAsync(
        RefactoringContext context,
        CSharpExtractionOptions options,
        CancellationToken cancellationToken = default);
}
