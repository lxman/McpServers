using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Analysis;
using DocumentServer.Core.Services.Analysis.Models;
using DocumentServer.Core.Services.Core;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Models;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DocumentMcp.McpTools;

/// <summary>
/// MCP tools for core document operations (load, extract, validate, compare)
/// </summary>
[McpServerToolType]
public class DocumentTools(
    ILogger<DocumentTools> logger,
    DocumentCache cache,
    DocumentLoaderFactory loaderFactory,
    DocumentProcessor processor,
    DocumentValidator validator,
    DocumentComparator comparator,
    MetadataExtractor metadataExtractor,
    OutputGuard outputGuard)
{
    [McpServerTool, DisplayName("load_document")]
    [Description("Load a document into memory. See skills/document/load-document.md only when using this tool")]
    public async Task<string> LoadDocument(string filePath, string? password = null, bool shouldCache = true)
    {
        try
        {
            logger.LogDebug("Loading document: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found" }, SerializerOptions.JsonOptionsIndented);
            }

            // Check if already loaded
            LoadedDocument? cached = cache.Get(filePath);
            if (cached is not null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    filePath,
                    documentType = cached.DocumentType.ToString(),
                    isEncrypted = false,
                    isCached = true,
                    loadedAt = cached.LoadedAt
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Get appropriate loader
            IDocumentLoader? loader = loaderFactory.GetLoader(filePath);
            if (loader is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Unsupported document type" }, SerializerOptions.JsonOptionsIndented);
            }

            // Load document
            ServiceResult<LoadedDocument> result = await loader.LoadAsync(filePath, password);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error }, SerializerOptions.JsonOptionsIndented);
            }

            // Cache if requested
            if (shouldCache && result.Data is not null)
            {
                await cache.AddAsync(filePath, result.Data);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                documentType = result.Data?.DocumentType.ToString() ?? "Unknown",
                isEncrypted = !string.IsNullOrEmpty(password),
                isCached = shouldCache,
                loadedAt = result.Data?.LoadedAt ?? DateTime.UtcNow
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading document: {FilePath}", filePath);
            return ex.ToErrorResponse(outputGuard, errorCode: "LOAD_DOCUMENT_FAILED");
        }
    }

    [McpServerTool, DisplayName("unload_document")]
    [Description("Unload a document from memory. See skills/document/unload-document.md only when using this tool")]
    public string UnloadDocument(string filePath)
    {
        try
        {
            logger.LogDebug("Unloading document: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            bool removed = cache.Remove(filePath);

            return JsonSerializer.Serialize(new
            {
                success = removed,
                filePath,
                message = removed ? "Document unloaded from memory" : "Document was not loaded"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unloading document: {FilePath}", filePath);
            return ex.ToErrorResponse(outputGuard, errorCode: "UNLOAD_DOCUMENT_FAILED");
        }
    }

    [McpServerTool, DisplayName("list_documents")]
    [Description("List all loaded documents. See skills/document/list-documents.md only when using this tool")]
    public string ListDocuments()
    {
        try
        {
            logger.LogDebug("Listing all loaded documents");

            List<string> cachedPaths = cache.GetCachedPaths();
            long totalMemory = cache.GetTotalMemoryUsage();

            var documents = new List<object>();
            foreach (string path in cachedPaths)
            {
                LoadedDocument? doc = cache.Get(path);
                if (doc is not null)
                {
                    documents.Add(new
                    {
                        filePath = path,
                        documentType = doc.DocumentType.ToString(),
                        lastModified = doc.LoadedAt,
                        sizeBytes = doc.MemorySizeBytes
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                loadedDocuments = cachedPaths,
                totalCount = cachedPaths.Count,
                totalMemoryMB = totalMemory / (1024.0 * 1024.0),
                documents
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing documents");
            return ex.ToErrorResponse(outputGuard, errorCode: "LIST_DOCUMENTS_FAILED");
        }
    }

    [McpServerTool, DisplayName("extract_content")]
    [Description("Extract text content from a document. See skills/document/extract-content.md only when using this tool")]
    public async Task<string> ExtractContent(
        string filePath,
        bool includeMetadata = false,
        int? startPage = null,
        int? endPage = null,
        int? maxPages = null)
    {
        try
        {
            logger.LogDebug("Extracting content from: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return outputGuard.CreateErrorResponse("File path is required", errorCode: "INVALID_PARAMETER");
            }

            ServiceResult<string> result = await processor.ExtractTextAsync(filePath, null, startPage, endPage, maxPages);

            if (!result.Success)
            {
                return outputGuard.CreateErrorResponse(result.Error ?? "Failed to extract content", errorCode: "EXTRACTION_FAILED");
            }

            Dictionary<string, string>? metadata = null;
            if (includeMetadata)
            {
                ServiceResult<EnrichedMetadata> metadataResult = await metadataExtractor.ExtractAsync(filePath);
                if (metadataResult is { Success: true, Data: not null })
                {
                    metadata = metadataResult.Data.DocumentMetadata;
                }
            }

            var response = new
            {
                success = true,
                filePath,
                content = result.Data ?? string.Empty,
                contentLength = result.Data?.Length ?? 0,
                metadata
            };

            string serialized = JsonSerializer.Serialize(response, SerializerOptions.JsonOptionsIndented);

            // Check response size - document extraction can return very large text content
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(serialized, "extract_content");

            if (!sizeCheck.IsWithinLimit)
            {
                int contentLength = result.Data?.Length ?? 0;
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Extracted content totals {sizeCheck.EstimatedTokens:N0} estimated tokens (content length: {contentLength:N0} characters), exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Use maxPages parameter to limit extraction (e.g., maxPages: 5 or 10)\n" +
                    "  2. Use startPage/endPage to extract specific page ranges\n" +
                    "  3. Extract document in smaller chunks (e.g., pages 1-10, then 11-20)\n" +
                    "  4. Use get_metadata first to check page count\n" +
                    "  5. Set includeMetadata to false to reduce response size",
                    new {
                        currentContentLength = contentLength,
                        currentMaxPages = maxPages,
                        suggestedMaxPages = Math.Max(1, (maxPages ?? 100) / 10),
                        startPage,
                        endPage,
                        filePath
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting content: {FilePath}", filePath);
            return ex.ToErrorResponse(outputGuard, errorCode: "EXTRACT_CONTENT_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_metadata")]
    [Description("Get metadata from a document. See skills/document/get-metadata.md only when using this tool")]
    public async Task<string> GetMetadata(string filePath)
    {
        try
        {
            logger.LogDebug("Getting metadata for: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            ServiceResult<EnrichedMetadata> result = await metadataExtractor.ExtractAsync(filePath);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath = result.Data?.FilePath,
                fileName = result.Data?.FileName,
                fileExtension = result.Data?.FileExtension,
                fileSizeBytes = result.Data?.FileSizeBytes,
                created = result.Data?.Created,
                modified = result.Data?.Modified,
                accessed = result.Data?.Accessed,
                documentType = result.Data?.DocumentType,
                isEncrypted = result.Data?.IsEncrypted,
                pageCount = result.Data?.PageCount,
                author = result.Data?.Author,
                title = result.Data?.Title,
                createdDate = result.Data?.CreatedDate,
                contentLength = result.Data?.ContentLength,
                wordCount = result.Data?.WordCount,
                lineCount = result.Data?.LineCount,
                documentMetadata = result.Data?.DocumentMetadata
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metadata: {FilePath}", filePath);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("validate_document")]
    [Description("Validate document structure and content. See skills/document/validate-document.md only when using this tool")]
    public async Task<string> ValidateDocument(string filePath)
    {
        try
        {
            logger.LogDebug("Validating document: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            ServiceResult<ValidationResult> result = await validator.ValidateAsync(filePath);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath = result.Data?.FilePath,
                isValid = result.Data?.IsValid ?? false,
                canOpen = result.Data?.CanOpen ?? false,
                isCorrupted = result.Data?.IsCorrupted ?? false,
                isEncrypted = result.Data?.IsEncrypted ?? false,
                documentType = result.Data?.DocumentType,
                fileSize = result.Data?.FileSize,
                lastModified = result.Data?.LastModified,
                contentLength = result.Data?.ContentLength,
                errors = result.Data?.Errors,
                warnings = result.Data?.Warnings,
                metadata = result.Data?.Metadata
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating document: {FilePath}", filePath);
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("compare_documents")]
    [Description("Compare two documents for differences. See skills/document/compare-documents.md only when using this tool")]
    public async Task<string> CompareDocuments(string filePath1, string filePath2)
    {
        try
        {
            logger.LogDebug("Comparing documents: {FilePath1} vs {FilePath2}", filePath1, filePath2);

            if (string.IsNullOrWhiteSpace(filePath1) || string.IsNullOrWhiteSpace(filePath2))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Both file paths are required" }, SerializerOptions.JsonOptionsIndented);
            }

            ServiceResult<ComparisonResult> result = await comparator.CompareAsync(filePath1, filePath2);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error }, SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath1 = result.Data?.FilePath1,
                filePath2 = result.Data?.FilePath2,
                document1Length = result.Data?.Document1Length,
                document2Length = result.Data?.Document2Length,
                document1WordCount = result.Data?.Document1WordCount,
                document2WordCount = result.Data?.Document2WordCount,
                characterSimilarity = result.Data?.CharacterSimilarity,
                commonWords = result.Data?.CommonWords,
                wordOverlapPercentage = result.Data?.WordOverlapPercentage,
                overallSimilarity = result.Data?.OverallSimilarity,
                areIdentical = result.Data?.AreIdentical ?? false,
                areSimilar = result.Data?.AreSimilar ?? false,
                similarityScore = result.Data?.SimilarityScore ?? 0,
                summary = result.Data?.Summary,
                differences = result.Data?.Differences
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing documents");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("clear_cache")]
    [Description("Clear all loaded documents from memory. See skills/document/clear-cache.md only when using this tool")]
    public string ClearCache()
    {
        try
        {
            logger.LogDebug("Clearing document cache");

            int countBefore = cache.GetCachedPaths().Count;
            long memoryBefore = cache.GetTotalMemoryUsage();

            cache.Clear();

            return JsonSerializer.Serialize(new
            {
                success = true,
                documentsCleared = countBefore,
                memoryReleasedMB = memoryBefore / (1024.0 * 1024.0),
                message = $"Cleared {countBefore} documents from cache"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing cache");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_document_status")]
    [Description("Get the status of the document processing system. See skills/document/get-status.md only when using this tool")]
    public string GetDocumentStatus()
    {
        try
        {
            logger.LogDebug("Getting document status");

            List<string> cachedPaths = cache.GetCachedPaths();
            long totalMemory = cache.GetTotalMemoryUsage();

            string[] supportedFormats =
            [
                "pdf", "docx", "doc", "xlsx", "xls", "pptx", "ppt",
                "txt", "rtf", "odt", "ods", "odp", "html", "xml", "json"
            ];

            return JsonSerializer.Serialize(new
            {
                success = true,
                status = "operational",
                loadedDocuments = cachedPaths.Count,
                totalMemoryMB = totalMemory / (1024.0 * 1024.0),
                supportedFormats,
                capabilities = new
                {
                    textExtraction = true,
                    metadataExtraction = true,
                    validation = true,
                    comparison = true,
                    ocr = true,
                    indexing = true,
                    search = true,
                    passwordProtected = true
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting status");
            return ex.ToErrorResponse(outputGuard, errorCode: "OPERATION_FAILED");
        }
    }
}