using Microsoft.AspNetCore.Mvc;
using DesktopCommander.Services.DocumentSearching;
using DesktopCommander.Services.DocumentSearching.Models;

namespace DesktopCommander.Controllers;

/// <summary>
/// Document indexing and search operations API
/// </summary>
[ApiController]
[Route("api/documents")]
public class DocumentController(
    DocumentProcessor documentProcessor,
    DocumentIndexer indexManager,
    PasswordManager passwordManager,
    OcrService ocrService,
    ILogger<DocumentController> logger) : ControllerBase

{
    /// <summary>
    /// Create a searchable index from a directory of documents
    /// </summary>
    [HttpPost("indexes")]
    public async Task<IActionResult> CreateIndex([FromBody] CreateIndexRequest request)
    {
        try
        {
            string rootPath = Path.GetFullPath(request.RootPath);
            
            if (!Directory.Exists(rootPath))
            {
                return NotFound(new { success = false, error = "Directory not found" });
            }
            
            IndexingOptions? options = null;
            if (!string.IsNullOrEmpty(request.Options))
            {
                options = System.Text.Json.JsonSerializer.Deserialize<IndexingOptions>(request.Options);
            }
            
            IndexingResult result = await indexManager.BuildIndex(
                request.IndexName,
                rootPath,
                options);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating index: {IndexName}", request.IndexName);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// List all available document indexes
    /// </summary>
    [HttpGet("indexes")]
    public async Task<IActionResult> ListIndexes()
    {
        try
        {
            List<string> result = await indexManager.GetIndexNames();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing indexes");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Search documents in an index
    /// </summary>
    [HttpPost("indexes/{indexName}/search")]
    public IActionResult SearchDocuments(
        string indexName,
        [FromBody] SearchDocumentsRequest request)
    {
        try
        {
            SearchResults result = indexManager.Search(
                request.Query,
                indexName,
                null);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching index: {IndexName}", indexName);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Extract content from a single document
    /// </summary>
    [HttpPost("extract")]
    public async Task<IActionResult> ExtractContent([FromBody] ExtractDocumentRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            DocumentContent result = await documentProcessor.ExtractContent(
                filePath,
                request.Password);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting content from: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed metadata from a document
    /// </summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata(
        [FromQuery] string filePath,
        [FromQuery] string? password = null)
    {
        try
        {
            filePath = Path.GetFullPath(filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            DocumentContent content = await documentProcessor.ExtractContent(filePath, password);
            var result = new 
            {
                success = true,
                metadata = content.Metadata,
                title = content.Title,
                documentType = content.DocumentType.ToString()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metadata from: {Path}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Discover documents in a directory without indexing
    /// </summary>
    [HttpPost("discover")]
    public IActionResult DiscoverDocuments([FromBody] DiscoverDocumentsRequest request)
    {
        try
        {
            string rootPath = Path.GetFullPath(request.RootPath);
            
            if (!Directory.Exists(rootPath))
            {
                return NotFound(new { success = false, error = "Directory not found" });
            }

            var documents = new List<FileInfo>();
            string[] patterns = request.IncludePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            SearchOption searchOption = request.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            foreach (string pattern in patterns)
            {
                try
                {
                    string[] files = Directory.GetFiles(rootPath, pattern.Trim(), searchOption);
                    documents.AddRange(files.Select(f => new FileInfo(f)));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error discovering files with pattern {Pattern}", pattern);
                }
            }

            var result = documents.Select(f => new
            {
                fileName = f.Name,
                filePath = f.FullName,
                fileSizeBytes = f.Length,
                lastModified = f.LastWriteTimeUtc
            }).ToList();


            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error discovering documents in: {Path}", request.RootPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Remove an index permanently
    /// </summary>
    [HttpDelete("indexes/{indexName}")]
    public IActionResult RemoveIndex(string indexName)
    {
        try
        {
            bool result = indexManager.RemoveIndex(indexName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing index: {IndexName}", indexName);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Unload an index from memory while keeping it discoverable
    /// </summary>
    [HttpPost("indexes/{indexName}/unload")]
    public IActionResult UnloadIndex(string indexName)
    {
        try
        {
            bool result = indexManager.UnloadIndex(indexName);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unloading index: {IndexName}", indexName);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Unload all indexes from memory
    /// </summary>
    [HttpPost("indexes/unload-all")]
    public IActionResult UnloadAllIndexes()
    {
        try
        {
            int result = indexManager.UnloadAllIndexes();

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unloading all indexes");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get memory status of all document indexes
    /// </summary>
    [HttpGet("indexes/memory-status")]
    public IActionResult GetMemoryStatus()
    {
        try
        {
            Task<Dictionary<string, IndexMemoryStatus>> result = indexManager.GetIndexMemoryStatus();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting memory status");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test search in an index to see content
    /// </summary>
    [HttpPost("indexes/{indexName}/test")]
    public IActionResult TestIndex(
        string indexName,
        [FromBody] TestIndexRequest request)
    {
        try
        {
            SearchResults result = indexManager.Search(request.TestQuery, indexName, null);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing index: {IndexName}", indexName);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Find the existing index for a directory
    /// </summary>
    [HttpGet("indexes/find")]
    public async Task<IActionResult> FindIndexForDirectory([FromQuery] string directoryPath)
    {
        try
        {
            directoryPath = Path.GetFullPath(directoryPath);
            // Since indexes don't store their source directory metadata,
            // we return all available indexes for the user to choose from
            List<string> availableIndexes = await indexManager.GetIndexNames();
            var result = new
            {
                success = true,
                message = "Indexes don't store source directory metadata. Here are all available indexes:",
                availableIndexes,
                suggestion = "Use the index name that you created for this directory, or create a new index if needed."
            };
            return Ok(result);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding index for: {Path}", directoryPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Register a password for a specific file
    /// </summary>
    [HttpPost("passwords/specific")]
    public IActionResult RegisterSpecificPassword([FromBody] RegisterSpecificPasswordRequest request)
    {
        try
        {
            string filePath = Path.GetFullPath(request.FilePath);
            passwordManager.RegisterSpecificPassword(filePath, request.Password);
            return Ok(new { success = true, message = "Password registered for file" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering password for: {Path}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Register a password pattern
    /// </summary>
    [HttpPost("passwords/pattern")]
    public IActionResult RegisterPasswordPattern([FromBody] RegisterPasswordPatternRequest request)
    {
        try
        {
            passwordManager.RegisterPasswordPattern(request.Pattern, request.Password);
            return Ok(new { success = true, message = "Password pattern registered" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering password pattern: {Pattern}", request.Pattern);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Auto-detect passwords from password files
    /// </summary>
    [HttpPost("passwords/auto-detect")]
    public IActionResult AutoDetectPasswords([FromBody] AutoDetectPasswordsRequest request)
    {
        try
        {
            string rootPath = Path.GetFullPath(request.RootPath);
            Task result = passwordManager.AutoDetectPasswordFiles(rootPath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error auto-detecting passwords in: {Path}", request.RootPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Bulk register passwords from JSON
    /// </summary>
    [HttpPost("passwords/bulk")]
    public IActionResult BulkRegisterPasswords([FromBody] BulkRegisterPasswordsRequest request)
    {
        try
        {
            // Parse the JSON and register each pattern individually
            var passwordMap = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.PasswordMapJson);
            if (passwordMap == null)
            {
                return BadRequest(new { success = false, error = "Invalid JSON format" });
            }

            foreach (KeyValuePair<string, string> kvp in passwordMap)
            {
                passwordManager.RegisterPasswordPattern(kvp.Key, kvp.Value);
            }

            return Ok(new { success = true, message = "Passwords registered" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error bulk registering passwords");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get registered password patterns (passwords are masked)
    /// </summary>
    [HttpGet("passwords")]
    public IActionResult GetRegisteredPasswords()
    {
        try
        {
            Dictionary<string, string> result = passwordManager.GetRegisteredPatterns();

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting registered passwords");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Check if a PDF is scanned and needs OCR
    /// </summary>
    [HttpPost("pdf/check-scanned")]
    public async Task<IActionResult> CheckPdfForScannedContent([FromBody] CheckPdfRequest request)
    {
        try
        {
            string pdfPath = Path.GetFullPath(request.PdfPath);
            
            if (!System.IO.File.Exists(pdfPath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            bool isScanned = ocrService.IsPdfScanned(pdfPath, request.Password);
            var result = new
            {
                success = true,
                isScanned,
                ocrAvailable = ocrService.IsAvailable,
                message = isScanned ? "PDF appears to be scanned and may need OCR" : "PDF contains extractable text"
            };
            return Ok(result);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking PDF: {Path}", request.PdfPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Extract text from scanned PDF using OCR
    /// </summary>
    [HttpPost("pdf/extract-scanned")]
    public async Task<IActionResult> ExtractTextFromScannedPdf([FromBody] ExtractScannedPdfRequest request)
    {
        try
        {
            string pdfPath = Path.GetFullPath(request.PdfPath);
            
            if (!System.IO.File.Exists(pdfPath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            string extractedText = await ocrService.ExtractTextFromScannedPdf(pdfPath, request.Password);
            var result = new
            {
                success = true,
                text = extractedText,
                ocrAvailable = ocrService.IsAvailable
            };
            return Ok(result);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting text from scanned PDF: {Path}", request.PdfPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Extract text from an image using OCR
    /// </summary>
    [HttpPost("image/extract-text")]
    public async Task<IActionResult> ExtractTextFromImage([FromBody] ExtractImageTextRequest request)
    {
        try
        {
            string imagePath = Path.GetFullPath(request.ImagePath);
            
            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound(new { success = false, error = "File not found" });
            }

            string extractedText = await ocrService.ExtractTextFromImage(imagePath);
            var result = new
            {
                success = true,
                text = extractedText,
                ocrAvailable = ocrService.IsAvailable
            };
            return Ok(result);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting text from image: {Path}", request.ImagePath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get OCR service status
    /// </summary>
    [HttpGet("ocr/status")]
    public IActionResult GetOcrStatus()
    {
        try
        {
            var result = new
            {
                success = true,
                ocrAvailable = ocrService.IsAvailable,
                message = ocrService.IsAvailable 
                    ? "OCR service is available and ready to use" 
                    : "OCR service is not available. Please install Tesseract OCR."
            };
            return Ok(result);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting OCR status");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

// Request models
public record CreateIndexRequest(string IndexName, string RootPath, string? Options = null);
public record SearchDocumentsRequest(string Query, string? Options = null);
public record ExtractDocumentRequest(string FilePath, string? Password = null);
public record DiscoverDocumentsRequest(string RootPath, string IncludePatterns = "*", bool Recursive = true);
public record TestIndexRequest(string TestQuery);
public record RegisterSpecificPasswordRequest(string FilePath, string Password);
public record RegisterPasswordPatternRequest(string Pattern, string Password);
public record AutoDetectPasswordsRequest(string RootPath);
public record BulkRegisterPasswordsRequest(string PasswordMapJson);
public record CheckPdfRequest(string PdfPath, string? Password = null);
public record ExtractScannedPdfRequest(string PdfPath, string? Password = null);
public record ExtractImageTextRequest(string ImagePath);