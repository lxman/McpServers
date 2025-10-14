using DocumentServer.Models.Common;

namespace DocumentServer.Services.Core;

/// <summary>
/// Interface for content extractors that handle specific document formats
/// </summary>
public interface IContentExtractor
{
    /// <summary>
    /// The document type this extractor supports
    /// </summary>
    DocumentType SupportedType { get; }

    /// <summary>
    /// Extract all text content from a loaded document
    /// </summary>
    /// <param name="document">The loaded document</param>
    /// <returns>Service result containing the extracted text or error information</returns>
    Task<ServiceResult<string>> ExtractTextAsync(LoadedDocument document);

    /// <summary>
    /// Extract metadata from a loaded document
    /// </summary>
    /// <param name="document">The loaded document</param>
    /// <returns>Service result containing metadata dictionary or error information</returns>
    Task<ServiceResult<Dictionary<string, string>>> ExtractMetadataAsync(LoadedDocument document);

    /// <summary>
    /// Extract structured content specific to the document type
    /// </summary>
    /// <param name="document">The loaded document</param>
    /// <returns>Service result containing structured content or error information</returns>
    Task<ServiceResult<object>> ExtractStructuredContentAsync(LoadedDocument document);
}
