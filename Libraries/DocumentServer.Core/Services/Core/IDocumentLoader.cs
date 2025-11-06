using DocumentServer.Core.Models.Common;

namespace DocumentServer.Core.Services.Core;

/// <summary>
/// Interface for document loaders that handle specific document formats
/// </summary>
public interface IDocumentLoader
{
    /// <summary>
    /// The document type this loader supports
    /// </summary>
    DocumentType SupportedType { get; }

    /// <summary>
    /// Check if this loader can handle the specified file
    /// </summary>
    /// <param name="filePath">Full path to the file</param>
    /// <returns>True if this loader can handle the file, otherwise false</returns>
    bool CanLoad(string filePath);

    /// <summary>
    /// Load a document into memory
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing the loaded document or error information</returns>
    Task<ServiceResult<LoadedDocument>> LoadAsync(string filePath, string? password = null);

    /// <summary>
    /// Extract all text content from a loaded document
    /// </summary>
    /// <param name="document">The loaded document</param>
    /// <returns>Service result containing the extracted text or error information</returns>
    Task<ServiceResult<string>> ExtractTextAsync(LoadedDocument document);

    /// <summary>
    /// Get document information and metadata
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing document information or error information</returns>
    Task<ServiceResult<DocumentInfo>> GetDocumentInfoAsync(string filePath, string? password = null);
}
