using DocumentServer.Models.Common;
using DocumentServer.Services.Analysis.Models;
using DocumentServer.Services.Core;

namespace DocumentServer.Services.Analysis;

/// <summary>
/// Service for extracting and enriching document metadata
/// </summary>
public class MetadataExtractor
{
    private readonly ILogger<MetadataExtractor> _logger;
    private readonly DocumentProcessor _processor;

    /// <summary>
    /// Initializes a new instance of the MetadataExtractor
    /// </summary>
    public MetadataExtractor(ILogger<MetadataExtractor> logger, DocumentProcessor processor)
    {
        _logger = logger;
        _processor = processor;
        _logger.LogInformation("MetadataExtractor initialized");
    }

    /// <summary>
    /// Extract comprehensive metadata from a document
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing enriched metadata</returns>
    public async Task<ServiceResult<EnrichedMetadata>> ExtractAsync(string filePath, string? password = null)
    {
        _logger.LogInformation("Extracting metadata from: {FilePath}", filePath);

        try
        {
            var enrichedMetadata = new EnrichedMetadata
            {
                FilePath = filePath
            };

            // Get file system metadata
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                enrichedMetadata.FileName = fileInfo.Name;
                enrichedMetadata.FileExtension = fileInfo.Extension;
                enrichedMetadata.FileSizeBytes = fileInfo.Length;
                enrichedMetadata.Created = fileInfo.CreationTime;
                enrichedMetadata.Modified = fileInfo.LastWriteTime;
                enrichedMetadata.Accessed = fileInfo.LastAccessTime;
            }
            else
            {
                return ServiceResult<EnrichedMetadata>.CreateFailure("File not found");
            }

            // Get document info
            ServiceResult<DocumentInfo> infoResult = await _processor.GetDocumentInfoAsync(filePath, password);
            if (infoResult.Success)
            {
                DocumentInfo info = infoResult.Data!;
                enrichedMetadata.DocumentType = info.DocumentType.ToString();
                enrichedMetadata.IsEncrypted = info.IsEncrypted;
                enrichedMetadata.PageCount = info.PageCount;
                enrichedMetadata.Author = info.Author;
                enrichedMetadata.Title = info.Title;
                enrichedMetadata.CreatedDate = info.CreatedDate;

                if (info.Metadata is not null)
                {
                    enrichedMetadata.DocumentMetadata = info.Metadata;
                }
            }

            // Get detailed metadata from extractor
            ServiceResult<Dictionary<string, string>> detailedResult = 
                await _processor.ExtractMetadataAsync(filePath, password);
            if (detailedResult is { Success: true, Data: not null })
            {
                // Merge detailed metadata
                foreach ((string key, string value) in detailedResult.Data)
                {
                    if (!enrichedMetadata.DocumentMetadata.ContainsKey(key))
                    {
                        enrichedMetadata.DocumentMetadata[key] = value;
                    }
                }
            }

            // Calculate content statistics if possible
            ServiceResult<string> textResult = await _processor.ExtractTextAsync(filePath, password);
            if (textResult.Success)
            {
                string text = textResult.Data!;
                enrichedMetadata.ContentLength = text.Length;
                
                string[] words = text.Split([' ', '\n', '\r', '\t'], 
                    StringSplitOptions.RemoveEmptyEntries);
                enrichedMetadata.WordCount = words.Length;
                
                string[] lines = text.Split('\n');
                enrichedMetadata.LineCount = lines.Length;
            }

            _logger.LogInformation("Metadata extraction complete: {FilePath}, Fields={Count}",
                filePath, enrichedMetadata.DocumentMetadata.Count);

            return ServiceResult<EnrichedMetadata>.CreateSuccess(enrichedMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from: {FilePath}", filePath);
            return ServiceResult<EnrichedMetadata>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract metadata from multiple documents
    /// </summary>
    /// <param name="filePaths">List of file paths</param>
    /// <param name="passwords">Optional dictionary mapping file paths to passwords</param>
    /// <returns>Service result containing metadata for all documents</returns>
    public async Task<ServiceResult<List<EnrichedMetadata>>> ExtractBatchAsync(
        List<string> filePaths,
        Dictionary<string, string>? passwords = null)
    {
        _logger.LogInformation("Extracting metadata from {Count} documents", filePaths.Count);

        try
        {
            var results = new List<EnrichedMetadata>();
            var successCount = 0;
            var failureCount = 0;

            foreach (string filePath in filePaths)
            {
                string? password = passwords?.GetValueOrDefault(filePath);
                
                ServiceResult<EnrichedMetadata> result = await ExtractAsync(filePath, password);
                if (result is { Success: true, Data: not null })
                {
                    results.Add(result.Data);
                    successCount++;
                }
                else
                {
                    _logger.LogWarning("Failed to extract metadata from: {FilePath}, Error: {Error}",
                        filePath, result.Error);
                    failureCount++;
                }
            }

            _logger.LogInformation("Batch metadata extraction complete: {Success} succeeded, {Failed} failed",
                successCount, failureCount);

            return ServiceResult<List<EnrichedMetadata>>.CreateSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch metadata extraction");
            return ServiceResult<List<EnrichedMetadata>>.CreateFailure(ex);
        }
    }
}
