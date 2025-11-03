using DocumentServer.Models.Common;

namespace DocumentServer.Services.Core;

/// <summary>
/// Central processor for document operations - coordinates loading, extraction, and caching
/// </summary>
public class DocumentProcessor
{
    private readonly ILogger<DocumentProcessor> _logger;
    private readonly DocumentCache _cache;
    private readonly DocumentLoaderFactory _loaderFactory;
    private readonly PasswordManager _passwordManager;
    private readonly IEnumerable<IContentExtractor> _extractors;

    /// <summary>
    /// Initializes a new instance of the DocumentProcessor
    /// </summary>
    public DocumentProcessor(
        ILogger<DocumentProcessor> logger,
        DocumentCache cache,
        DocumentLoaderFactory loaderFactory,
        PasswordManager passwordManager,
        IEnumerable<IContentExtractor> extractors)
    {
        _logger = logger;
        _cache = cache;
        _loaderFactory = loaderFactory;
        _passwordManager = passwordManager;
        _extractors = extractors;

        _logger.LogInformation("DocumentProcessor initialized with {ExtractorCount} extractors",
            _extractors.Count());
    }

    /// <summary>
    /// Load a document into cache
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing the loaded document or error information</returns>
    public async Task<ServiceResult<LoadedDocument>> LoadDocumentAsync(string filePath, string? password = null)
    {
        _logger.LogInformation("Loading document: {FilePath}", filePath);

        try
        {
            // Check if already cached
            LoadedDocument? cached = _cache.Get(filePath);
            if (cached is not null)
            {
                _logger.LogDebug("Document already cached: {FilePath}", filePath);
                return ServiceResult<LoadedDocument>.CreateSuccess(cached);
            }

            // Get appropriate loader
            IDocumentLoader? loader = _loaderFactory.GetLoader(filePath);
            if (loader is null)
            {
                string extension = Path.GetExtension(filePath);
                _logger.LogWarning("No loader found for: {FilePath}, Extension: {Extension}",
                    filePath, extension);
                return ServiceResult<LoadedDocument>.CreateFailure(
                    $"Unsupported file type: {extension}");
            }

            // Try to get password if not provided
            password ??= _passwordManager.GetPasswordForFile(filePath);

            // Load the document
            ServiceResult<LoadedDocument> loadResult = await loader.LoadAsync(filePath, password);
            if (!loadResult.Success)
            {
                _logger.LogWarning("Failed to load document: {FilePath}, Error: {Error}",
                    filePath, loadResult.Error);
                return loadResult;
            }

            // Add to cache
            bool cached_success = await _cache.AddAsync(filePath, loadResult.Data!);
            if (!cached_success)
            {
                _logger.LogWarning("Failed to cache document (cache full): {FilePath}", filePath);
                // Still return success - the document is loaded, just not cached
            }

            _logger.LogInformation("Document loaded successfully: {FilePath}, Type={Type}",
                filePath, loadResult.Data!.DocumentType);

            return loadResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading document: {FilePath}", filePath);
            return ServiceResult<LoadedDocument>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract text content from a document (loads if not cached)
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <param name="startPage">Starting page number (1-based, null = from beginning)</param>
    /// <param name="endPage">Ending page number (1-based, inclusive, null = to end)</param>
    /// <param name="maxPages">Maximum number of pages to extract (alternative to startPage/endPage)</param>
    /// <returns>Service result containing the extracted text or error information</returns>
    public async Task<ServiceResult<string>> ExtractTextAsync(string filePath, string? password = null, int? startPage = null, int? endPage = null, int? maxPages = null)
    {
        _logger.LogInformation("Extracting text from: {FilePath}", filePath);

        try
        {
            // Ensure document is loaded
            ServiceResult<LoadedDocument> loadResult = await LoadDocumentAsync(filePath, password);
            if (!loadResult.Success)
            {
                return ServiceResult<string>.CreateFailure(loadResult.Error!);
            }

            LoadedDocument document = loadResult.Data!;

            // Get appropriate extractor
            IContentExtractor? extractor = _extractors.FirstOrDefault(e =>
                e.SupportedType == document.DocumentType ||
                (e.SupportedType == DocumentType.Word && (
                    document.DocumentType == DocumentType.Word ||
                    document.DocumentType == DocumentType.Excel ||
                    document.DocumentType == DocumentType.PowerPoint)));

            if (extractor is null)
            {
                _logger.LogWarning("No extractor found for document type: {Type}", document.DocumentType);
                return ServiceResult<string>.CreateFailure(
                    $"No extractor available for {document.DocumentType}");
            }

            // Extract text with page range parameters
            ServiceResult<string> extractResult = await extractor.ExtractTextAsync(document, startPage, endPage, maxPages);
            
            if (extractResult.Success)
            {
                _logger.LogInformation("Text extracted successfully: {FilePath}, Length={Length} characters",
                    filePath, extractResult.Data!.Length);
            }

            return extractResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from: {FilePath}", filePath);
            return ServiceResult<string>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract metadata from a document (loads if not cached)
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing metadata dictionary or error information</returns>
    public async Task<ServiceResult<Dictionary<string, string>>> ExtractMetadataAsync(
        string filePath, string? password = null)
    {
        _logger.LogInformation("Extracting metadata from: {FilePath}", filePath);

        try
        {
            // Ensure document is loaded
            ServiceResult<LoadedDocument> loadResult = await LoadDocumentAsync(filePath, password);
            if (!loadResult.Success)
            {
                return ServiceResult<Dictionary<string, string>>.CreateFailure(loadResult.Error!);
            }

            LoadedDocument document = loadResult.Data!;

            // Get appropriate extractor
            IContentExtractor? extractor = _extractors.FirstOrDefault(e =>
                e.SupportedType == document.DocumentType ||
                (e.SupportedType == DocumentType.Word && (
                    document.DocumentType == DocumentType.Word ||
                    document.DocumentType == DocumentType.Excel ||
                    document.DocumentType == DocumentType.PowerPoint)));

            if (extractor is null)
            {
                return ServiceResult<Dictionary<string, string>>.CreateFailure(
                    $"No extractor available for {document.DocumentType}");
            }

            // Extract metadata
            ServiceResult<Dictionary<string, string>> metadataResult = 
                await extractor.ExtractMetadataAsync(document);

            if (metadataResult.Success)
            {
                _logger.LogInformation("Metadata extracted successfully: {FilePath}, Fields={Count}",
                    filePath, metadataResult.Data!.Count);
            }

            return metadataResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from: {FilePath}", filePath);
            return ServiceResult<Dictionary<string, string>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract structured content from a document (loads if not cached)
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing structured content or error information</returns>
    public async Task<ServiceResult<object>> ExtractStructuredContentAsync(
        string filePath, string? password = null)
    {
        _logger.LogInformation("Extracting structured content from: {FilePath}", filePath);

        try
        {
            // Ensure document is loaded
            ServiceResult<LoadedDocument> loadResult = await LoadDocumentAsync(filePath, password);
            if (!loadResult.Success)
            {
                return ServiceResult<object>.CreateFailure(loadResult.Error!);
            }

            LoadedDocument document = loadResult.Data!;

            // Get appropriate extractor
            IContentExtractor? extractor = _extractors.FirstOrDefault(e =>
                e.SupportedType == document.DocumentType ||
                (e.SupportedType == DocumentType.Word && (
                    document.DocumentType == DocumentType.Word ||
                    document.DocumentType == DocumentType.Excel ||
                    document.DocumentType == DocumentType.PowerPoint)));

            if (extractor is null)
            {
                return ServiceResult<object>.CreateFailure(
                    $"No extractor available for {document.DocumentType}");
            }

            // Extract structured content
            ServiceResult<object> contentResult = await extractor.ExtractStructuredContentAsync(document);

            if (contentResult.Success)
            {
                _logger.LogInformation("Structured content extracted successfully: {FilePath}", filePath);
            }

            return contentResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting structured content from: {FilePath}", filePath);
            return ServiceResult<object>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Unload a document from cache
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>True if removed, false if not found</returns>
    public bool UnloadDocument(string filePath)
    {
        _logger.LogInformation("Unloading document: {FilePath}", filePath);
        bool removed = _cache.Remove(filePath);

        if (removed)
        {
            _logger.LogDebug("Document unloaded: {FilePath}", filePath);
        }
        else
        {
            _logger.LogDebug("Document not found in cache: {FilePath}", filePath);
        }

        return removed;
    }

    /// <summary>
    /// Clear all documents from cache
    /// </summary>
    /// <returns>Number of documents that were cached</returns>
    public int ClearAllDocuments()
    {
        _logger.LogInformation("Clearing all cached documents");
        int count = _cache.GetCount();
        _cache.Clear();
        _logger.LogInformation("Cleared {Count} documents from cache", count);
        return count;
    }

    /// <summary>
    /// Get list of all cached document paths
    /// </summary>
    /// <returns>List of file paths currently cached</returns>
    public List<string> GetCachedDocuments()
    {
        return _cache.GetCachedPaths();
    }

    /// <summary>
    /// Get document information without loading the full document
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing document information or error information</returns>
    public async Task<ServiceResult<DocumentInfo>> GetDocumentInfoAsync(
        string filePath, string? password = null)
    {
        _logger.LogInformation("Getting document info: {FilePath}", filePath);

        try
        {
            IDocumentLoader? loader = _loaderFactory.GetLoader(filePath);
            if (loader is null)
            {
                return ServiceResult<DocumentInfo>.CreateFailure(
                    $"Unsupported file type: {Path.GetExtension(filePath)}");
            }

            password ??= _passwordManager.GetPasswordForFile(filePath);

            return await loader.GetDocumentInfoAsync(filePath, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document info: {FilePath}", filePath);
            return ServiceResult<DocumentInfo>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    /// <returns>Dictionary containing cache statistics</returns>
    public Dictionary<string, object> GetCacheStatistics()
    {
        return _cache.GetStatistics();
    }

    /// <summary>
    /// Check if a file type is supported
    /// </summary>
    /// <param name="filePath">Full path to the file</param>
    /// <returns>True if supported, otherwise false</returns>
    public bool IsFileTypeSupported(string filePath)
    {
        return _loaderFactory.CanLoad(filePath);
    }

    /// <summary>
    /// Get list of supported file extensions
    /// </summary>
    /// <returns>List of supported extensions</returns>
    public List<string> GetSupportedExtensions()
    {
        return _loaderFactory.GetSupportedExtensions();
    }
}
