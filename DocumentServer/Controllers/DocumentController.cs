using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Models.Requests;
using DocumentServer.Core.Models.Responses;
using DocumentServer.Core.Services.Analysis;
using DocumentServer.Core.Services.Analysis.Models;
using DocumentServer.Core.Services.Core;
using Microsoft.AspNetCore.Mvc;

namespace DocumentServer.Controllers;

/// <summary>
/// Controller for core document operations (load, extract, validate, compare)
/// </summary>
[ApiController]
[Route("api/documents")]
public class DocumentController(
    ILogger<DocumentController> logger,
    DocumentCache cache,
    DocumentLoaderFactory loaderFactory,
    DocumentProcessor processor,
    DocumentValidator validator,
    DocumentComparator comparator,
    MetadataExtractor metadataExtractor)
    : ControllerBase
{
    /// <summary>
    /// Load a document into memory
    /// </summary>
    /// <param name="request">Load parameters</param>
    /// <returns>Load result with document information</returns>
    [HttpPost("load")]
    [ProducesResponseType(typeof(LoadDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoadDocumentResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoadDocument([FromBody] LoadDocumentRequest request)
    {
        logger.LogInformation("Loading document: {FilePath}", request.FilePath);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new LoadDocumentResponse
            {
                Success = false,
                Error = "File path is required"
            });
        }

        if (!System.IO.File.Exists(request.FilePath))
        {
            logger.LogWarning("File not found: {FilePath}", request.FilePath);
            return BadRequest(new LoadDocumentResponse
            {
                Success = false,
                FilePath = request.FilePath,
                Error = "File not found"
            });
        }

        try
        {
            // Check if already loaded
            LoadedDocument? cached = cache.Get(request.FilePath);
            if (cached is not null)
            {
                logger.LogInformation("Document already loaded from cache: {FilePath}", request.FilePath);
                return Ok(new LoadDocumentResponse
                {
                    Success = true,
                    FilePath = request.FilePath,
                    DocumentType = cached.DocumentType,
                    IsEncrypted = false,
                    IsCached = true,
                    LoadedAt = cached.LoadedAt
                });
            }

            // Get appropriate loader
            IDocumentLoader? loader = loaderFactory.GetLoader(request.FilePath);
            if (loader is null)
            {
                logger.LogWarning("Unsupported document type: {FilePath}", request.FilePath);
                return BadRequest(new LoadDocumentResponse
                {
                    Success = false,
                    FilePath = request.FilePath,
                    Error = "Unsupported document type"
                });
            }

            // Load document
            ServiceResult<LoadedDocument> result = await loader.LoadAsync(request.FilePath, request.Password);

            if (!result.Success)
            {
                logger.LogWarning("Failed to load document: {FilePath}, Error: {Error}", 
                    request.FilePath, result.Error);
                return BadRequest(new LoadDocumentResponse
                {
                    Success = false,
                    FilePath = request.FilePath,
                    Error = result.Error
                });
            }

            // Cache if requested
            if (request.Cache && result.Data is not null)
            {
                await cache.AddAsync(request.FilePath, result.Data);
            }

            logger.LogInformation("Document loaded successfully: {FilePath}, Type: {Type}", 
                request.FilePath, result.Data?.DocumentType);

            return Ok(new LoadDocumentResponse
            {
                Success = true,
                FilePath = request.FilePath,
                DocumentType = result.Data?.DocumentType ?? DocumentType.Unknown,
                IsEncrypted = !string.IsNullOrEmpty(request.Password),
                IsCached = request.Cache,
                LoadedAt = result.Data?.LoadedAt ?? DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading document: {FilePath}", request.FilePath);
            return BadRequest(new LoadDocumentResponse
            {
                Success = false,
                FilePath = request.FilePath,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Unload a document from memory
    /// </summary>
    /// <param name="filePath">Path to the document to unload</param>
    /// <returns>Success status</returns>
    [HttpDelete("unload")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult UnloadDocument([FromQuery] string filePath)
    {
        logger.LogInformation("Unloading document: {FilePath}", filePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { Success = false, Error = "File path is required" });
        }

        bool removed = cache.Remove(filePath);

        logger.LogInformation("Document unload result: {FilePath}, Success: {Success}", filePath, removed);

        return Ok(new
        {
            Success = removed,
            FilePath = filePath,
            Message = removed ? "Document unloaded from memory" : "Document was not loaded"
        });
    }

    /// <summary>
    /// List all loaded documents
    /// </summary>
    /// <returns>List of loaded documents with details</returns>
    [HttpGet]
    [ProducesResponseType(typeof(DocumentListResponse), StatusCodes.Status200OK)]
    public IActionResult ListDocuments()
    {
        logger.LogDebug("Listing all loaded documents");

        List<string> cachedPaths = cache.GetCachedPaths();
        long totalMemory = cache.GetTotalMemoryUsage();

        var documents = new Dictionary<string, DocumentInfo>();
        foreach (string path in cachedPaths)
        {
            LoadedDocument? doc = cache.Get(path);
            if (doc is not null)
            {
                documents[path] = new DocumentInfo
                {
                    FilePath = path,
                    DocumentType = doc.DocumentType,
                    LastModified = doc.LoadedAt,
                    SizeBytes = doc.MemorySizeBytes
                };
            }
        }

        var response = new DocumentListResponse
        {
            LoadedDocuments = cachedPaths,
            TotalCount = cachedPaths.Count,
            TotalMemoryMB = totalMemory / (1024.0 * 1024.0),
            Documents = documents
        };

        logger.LogInformation("Found {Count} loaded documents, {MemoryMB:F2} MB", 
            response.TotalCount, response.TotalMemoryMB);

        return Ok(response);
    }

    /// <summary>
    /// Extract text content from a document
    /// </summary>
    /// <param name="request">Extraction parameters</param>
    /// <returns>Extracted content</returns>
    [HttpPost("extract")]
    [ProducesResponseType(typeof(ExtractContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtractContentResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExtractContent([FromBody] ExtractContentRequest request)
    {
        logger.LogInformation("Extracting content from: {FilePath}, StartPage={StartPage}, EndPage={EndPage}, MaxPages={MaxPages}",
            request.FilePath, request.StartPage, request.EndPage, request.MaxPages);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new ExtractContentResponse
            {
                Success = false,
                Error = "File path is required"
            });
        }

        try
        {
            ServiceResult<string> result = await processor.ExtractTextAsync(request.FilePath, null, request.StartPage, request.EndPage, request.MaxPages);

            if (!result.Success)
            {
                logger.LogWarning("Failed to extract content: {FilePath}, Error: {Error}", 
                    request.FilePath, result.Error);
                return BadRequest(new ExtractContentResponse
                {
                    Success = false,
                    FilePath = request.FilePath,
                    Error = result.Error
                });
            }

            Dictionary<string, string>? metadata = null;
            if (request.IncludeMetadata)
            {
                ServiceResult<EnrichedMetadata> metadataResult = await metadataExtractor.ExtractAsync(request.FilePath);
                if (metadataResult is { Success: true, Data: not null })
                {
                    // Convert EnrichedMetadata to a simple dictionary
                    metadata = metadataResult.Data.DocumentMetadata;
                }
            }

            logger.LogInformation("Content extracted successfully: {FilePath}, Length: {Length} chars", 
                request.FilePath, result.Data?.Length ?? 0);

            return Ok(new ExtractContentResponse
            {
                Success = true,
                FilePath = request.FilePath,
                Content = result.Data ?? string.Empty,
                ContentLength = result.Data?.Length ?? 0,
                Metadata = metadata
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting content: {FilePath}", request.FilePath);
            return BadRequest(new ExtractContentResponse
            {
                Success = false,
                FilePath = request.FilePath,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Test endpoint to verify request parameter deserialization
    /// </summary>
    [HttpPost("test-params")]
    public IActionResult TestParams([FromBody] ExtractContentRequest request)
    {
        return Ok(new
        {
            FilePath = request.FilePath,
            IncludeMetadata = request.IncludeMetadata,
            StartPage = request.StartPage,
            EndPage = request.EndPage,
            MaxPages = request.MaxPages
        });
    }

    /// <summary>
    /// Get metadata from a document
    /// </summary>
    /// <param name="filePath">Path to the document</param>
    /// <returns>Document metadata</returns>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMetadata([FromQuery] string filePath)
    {
        logger.LogInformation("Getting metadata from: {FilePath}", filePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { Error = "File path is required" });
        }

        try
        {
            ServiceResult<EnrichedMetadata> result = await metadataExtractor.ExtractAsync(filePath);

            if (!result.Success)
            {
                logger.LogWarning("Failed to extract metadata: {FilePath}, Error: {Error}", 
                    filePath, result.Error);
                return BadRequest(new { result.Error });
            }

            logger.LogInformation("Metadata extracted successfully: {FilePath}", filePath);

            return Ok(result.Data?.DocumentMetadata ?? new Dictionary<string, string>());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting metadata: {FilePath}", filePath);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Validate a document
    /// </summary>
    /// <param name="request">Validation parameters</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateDocumentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateDocument([FromBody] ValidateDocumentRequest request)
    {
        logger.LogInformation("Validating document: {FilePath}", request.FilePath);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new ValidateDocumentResponse
            {
                IsValid = false,
                FilePath = request.FilePath,
                Errors = ["File path is required"]
            });
        }

        try
        {
            ServiceResult<ValidationResult> result = await validator.ValidateAsync(request.FilePath);
            bool canOpen = await validator.CanOpenAsync(request.FilePath);

            var response = new ValidateDocumentResponse
            {
                IsValid = result is { Success: true, Data.IsValid: true },
                FilePath = request.FilePath,
                CanOpen = canOpen,
                IsCorrupted = result.Data?.IsCorrupted ?? false
            };

            if (result.Data is not null)
            {
                response.Errors.AddRange(result.Data.Errors);
                response.Warnings.AddRange(result.Data.Warnings);
            }

            if (!result.Success)
            {
                response.Errors.Add(result.Error ?? "Validation failed");
            }

            logger.LogInformation("Validation complete: {FilePath}, Valid: {IsValid}, CanOpen: {CanOpen}", 
                request.FilePath, response.IsValid, response.CanOpen);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating document: {FilePath}", request.FilePath);
            return Ok(new ValidateDocumentResponse
            {
                IsValid = false,
                FilePath = request.FilePath,
                Errors = [ex.Message]
            });
        }
    }

    /// <summary>
    /// Compare two documents
    /// </summary>
    /// <param name="request">Comparison parameters</param>
    /// <returns>Comparison result</returns>
    [HttpPost("compare")]
    [ProducesResponseType(typeof(CompareDocumentsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompareDocuments([FromBody] CompareDocumentsRequest request)
    {
        logger.LogInformation("Comparing documents: {File1} vs {File2}", 
            request.FilePath1, request.FilePath2);

        if (string.IsNullOrWhiteSpace(request.FilePath1) || string.IsNullOrWhiteSpace(request.FilePath2))
        {
            return BadRequest(new CompareDocumentsResponse
            {
                Success = false,
                Error = "Both file paths are required"
            });
        }

        try
        {
            ServiceResult<ComparisonResult> result = await comparator.CompareAsync(request.FilePath1, request.FilePath2);

            if (!result.Success)
            {
                logger.LogWarning("Comparison failed: {File1} vs {File2}, Error: {Error}", 
                    request.FilePath1, request.FilePath2, result.Error);
                return BadRequest(new CompareDocumentsResponse
                {
                    Success = false,
                    FilePath1 = request.FilePath1,
                    FilePath2 = request.FilePath2,
                    Error = result.Error
                });
            }

            ComparisonResult? compResult = result.Data;
            var response = new CompareDocumentsResponse
            {
                Success = true,
                FilePath1 = request.FilePath1,
                FilePath2 = request.FilePath2,
                AreIdentical = compResult?.AreIdentical ?? false,
                SimilarityScore = compResult?.SimilarityScore ?? 0.0,
                Summary = compResult?.Summary ?? string.Empty
            };

            if (request.IncludeDetails && compResult?.Differences is not null)
            {
                response.Differences = compResult.Differences;
            }

            logger.LogInformation("Comparison complete: Identical: {Identical}, Similarity: {Similarity:P2}", 
                response.AreIdentical, response.SimilarityScore);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing documents: {File1} vs {File2}", 
                request.FilePath1, request.FilePath2);
            return BadRequest(new CompareDocumentsResponse
            {
                Success = false,
                FilePath1 = request.FilePath1,
                FilePath2 = request.FilePath2,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Clear all loaded documents from memory
    /// </summary>
    /// <returns>Result with count of cleared documents</returns>
    [HttpDelete("clear")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult ClearAll()
    {
        logger.LogInformation("Clearing all loaded documents");

        int count = cache.GetCachedPaths().Count;
        cache.Clear();

        logger.LogInformation("Cleared {Count} documents from memory", count);

        return Ok(new
        {
            Success = true,
            ClearedCount = count,
            Message = $"Cleared {count} documents from memory"
        });
    }

    /// <summary>
    /// Get service status and statistics
    /// </summary>
    /// <returns>Service status information</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        logger.LogDebug("Getting service status");

        int loadedCount = cache.GetCachedPaths().Count;
        long memoryBytes = cache.GetTotalMemoryUsage();
        double memoryMB = memoryBytes / (1024.0 * 1024.0);

        var status = new Dictionary<string, object>
        {
            ["service"] = "DocumentServer",
            ["status"] = "running",
            ["loadedDocuments"] = loadedCount,
            ["memoryUsageMB"] = Math.Round(memoryMB, 2),
            ["timestamp"] = DateTime.UtcNow
        };

        return Ok(status);
    }
}
