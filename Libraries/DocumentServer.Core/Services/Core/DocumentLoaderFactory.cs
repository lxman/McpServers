using DocumentServer.Core.Models.Common;
using Microsoft.Extensions.Logging;

namespace DocumentServer.Core.Services.Core;

/// <summary>
/// Factory for creating appropriate document loaders based on file type
/// </summary>
public class DocumentLoaderFactory
{
    private readonly IEnumerable<IDocumentLoader> _loaders;
    private readonly ILogger<DocumentLoaderFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the DocumentLoaderFactory
    /// </summary>
    /// <param name="loaders">All registered document loaders</param>
    /// <param name="logger">Logger instance</param>
    public DocumentLoaderFactory(IEnumerable<IDocumentLoader> loaders, ILogger<DocumentLoaderFactory> logger)
    {
        _loaders = loaders.ToList();
        _logger = logger;
        
        _logger.LogInformation("DocumentLoaderFactory initialized with {Count} loaders", _loaders.Count());
        
        foreach (var loader in _loaders)
        {
            _logger.LogDebug("Registered loader: {LoaderType} for {DocumentType}",
                loader.GetType().Name, loader.SupportedType);
        }
    }

    /// <summary>
    /// Get the appropriate document loader for a file
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>The appropriate loader if found, otherwise null</returns>
    public IDocumentLoader? GetLoader(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("Empty file path provided to GetLoader");
            return null;
        }

        var loader = _loaders.FirstOrDefault(l => l.CanLoad(filePath));

        if (loader is null)
        {
            var extension = Path.GetExtension(filePath);
            _logger.LogWarning("No loader found for file: {FilePath}, Extension: {Extension}",
                filePath, extension);
        }
        else
        {
            _logger.LogDebug("Found loader {LoaderType} for: {FilePath}",
                loader.GetType().Name, filePath);
        }

        return loader;
    }

    /// <summary>
    /// Get the appropriate document loader for a specific document type
    /// </summary>
    /// <param name="documentType">The document type</param>
    /// <returns>The appropriate loader if found, otherwise null</returns>
    public IDocumentLoader? GetLoaderForType(DocumentType documentType)
    {
        var loader = _loaders.FirstOrDefault(l => l.SupportedType == documentType);

        if (loader is null)
        {
            _logger.LogWarning("No loader found for document type: {DocumentType}", documentType);
        }
        else
        {
            _logger.LogDebug("Found loader {LoaderType} for document type: {DocumentType}",
                loader.GetType().Name, documentType);
        }

        return loader;
    }

    /// <summary>
    /// Check if a file can be loaded
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>True if a loader exists for this file, otherwise false</returns>
    public bool CanLoad(string filePath)
    {
        return GetLoader(filePath) is not null;
    }

    /// <summary>
    /// Get list of all supported file extensions
    /// </summary>
    /// <returns>List of supported extensions (e.g., ".pdf", ".docx")</returns>
    public List<string> GetSupportedExtensions()
    {
        var extensions = new List<string>();

        // Common extensions for each loader type
        foreach (var loader in _loaders)
        {
            switch (loader.SupportedType)
            {
                case DocumentType.Pdf:
                    extensions.Add(".pdf");
                    break;
                case DocumentType.Word:
                    extensions.AddRange([".docx", ".doc"]);
                    break;
                case DocumentType.Excel:
                    extensions.AddRange([".xlsx", ".xls", ".xlsm"]);
                    break;
                case DocumentType.PowerPoint:
                    extensions.AddRange([".pptx", ".ppt"]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return extensions.Distinct().ToList();
    }

    /// <summary>
    /// Get statistics about registered loaders
    /// </summary>
    /// <returns>Dictionary containing loader statistics</returns>
    public Dictionary<string, object> GetStatistics()
    {
        return new Dictionary<string, object>
        {
            ["LoaderCount"] = _loaders.Count(),
            ["SupportedTypes"] = _loaders.Select(l => l.SupportedType.ToString()).Distinct().ToList(),
            ["SupportedExtensions"] = GetSupportedExtensions(),
            ["Loaders"] = _loaders.Select(l => new
            {
                Type = l.GetType().Name,
                SupportedType = l.SupportedType.ToString()
            }).ToList()
        };
    }
}
