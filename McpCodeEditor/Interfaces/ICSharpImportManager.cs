using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for C# import (using statement) management operations.
/// Provides services for organizing, adding, and managing C# using statements.
/// </summary>
public interface ICSharpImportManager
{
    /// <summary>
    /// Organize using statements in a C# file by sorting alphabetically and removing duplicates.
    /// </summary>
    /// <param name="filePath">Path to the C# file</param>
    /// <param name="options">Options for organizing imports</param>
    /// <param name="previewOnly">If true, only preview changes without applying them</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the import organization operation</returns>
    Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        CSharpImportOperation options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a using statement to a C# file if it doesn't already exist.
    /// </summary>
    /// <param name="filePath">Path to the C# file</param>
    /// <param name="usingNamespace">Namespace to add (e.g., 'System.Collections.Generic')</param>
    /// <param name="previewOnly">If true, only preview changes without applying them</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the add using operation</returns>
    Task<RefactoringResult> AddUsingAsync(
        string filePath,
        string usingNamespace,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove unused using statements from a C# file.
    /// This is a placeholder for future semantic analysis implementation.
    /// </summary>
    /// <param name="filePath">Path to the C# file</param>
    /// <param name="previewOnly">If true, only preview changes without applying them</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the remove unused imports operation</returns>
    Task<RefactoringResult> RemoveUnusedImportsAsync(
        string filePath,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about current using statements in a C# file.
    /// </summary>
    /// <param name="filePath">Path to the C# file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Information about using statements</returns>
    Task<CSharpImportAnalysis> AnalyzeImportsAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
