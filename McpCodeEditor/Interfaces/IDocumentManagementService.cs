using Microsoft.CodeAnalysis;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service responsible for managing documents within Roslyn workspaces
/// Extracted from SymbolNavigationService - Phase 4 Task 2b
/// </summary>
public interface IDocumentManagementService
{
    /// <summary>
    /// Get a document from the current workspace by file path
    /// </summary>
    /// <param name="filePath">Full path to the file</param>
    /// <returns>Document if found, null otherwise</returns>
    Document? GetDocument(string filePath);
    
    /// <summary>
    /// Add a document to the fallback workspace if using fallback mode
    /// </summary>
    /// <param name="filePath">Path to the file to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added document, or null if failed/not applicable</returns>
    Task<Document?> AddDocumentToFallbackWorkspaceAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Try to resolve a document, adding it to fallback workspace if necessary
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document if found or successfully added, null otherwise</returns>
    Task<Document?> ResolveDocumentAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a file path exists and is valid for document operations
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <returns>True if the file exists and can be processed</returns>
    bool IsValidDocumentPath(string filePath);
}
