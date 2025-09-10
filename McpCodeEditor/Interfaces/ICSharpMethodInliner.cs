using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Models.Validation;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for C# method inlining operations.
/// Responsible for replacing method call sites with the method body and removing the method definition.
/// </summary>
public interface ICSharpMethodInliner
{
    /// <summary>
    /// Inline a C# method by replacing all call sites with the method body and removing the method definition.
    /// This operation improves performance by eliminating method call overhead for simple methods.
    /// </summary>
    /// <param name="filePath">Path to the C# file containing the method to inline</param>
    /// <param name="options">Method inlining options and configuration</param>
    /// <param name="previewOnly">If true, only preview changes without applying them</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing success status, changes made, and any error messages</returns>
    /// <remarks>
    /// Current limitations:
    /// - Only supports simple void methods without parameters
    /// - Does not support methods with return values
    /// - Expression-bodied and abstract methods are not supported
    /// - Requires at least one call site to be found
    /// </remarks>
    Task<RefactoringResult> InlineMethodAsync(
        string filePath,
        MethodInliningOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate if a method can be inlined before attempting the operation.
    /// Checks for common issues that would prevent successful inlining.
    /// </summary>
    /// <param name="filePath">Path to the C# file containing the method</param>
    /// <param name="methodName">Name of the method to validate for inlining</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Validation result with success status and any validation messages</returns>
    Task<MethodInliningValidationResult> ValidateMethodForInliningAsync(
        string filePath,
        string methodName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a method to help users understand what will be inlined.
    /// </summary>
    /// <param name="filePath">Path to the C# file containing the method</param>
    /// <param name="methodName">Name of the method to analyze</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Method information including call sites, complexity, and recommendations</returns>
    Task<MethodInliningInfo> GetMethodInliningInfoAsync(
        string filePath,
        string methodName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a method for inlining analysis
/// </summary>
public class MethodInliningInfo
{
    public string MethodName { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
    public int CallSitesCount { get; set; }
    public List<string> CallSiteLocations { get; set; } = [];
    public int MethodBodyLines { get; set; }
    public bool HasReturnStatements { get; set; }
    public bool IsExpressionBodied { get; set; }
    public bool CanBeInlined { get; set; }
    public string? InliningRecommendation { get; set; }
}
