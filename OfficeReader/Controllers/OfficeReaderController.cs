using Microsoft.AspNetCore.Mvc;
using OfficeReader.Models;
using OfficeReader.Models.Results;
using OfficeReader.Services;

namespace OfficeReader.Controllers;

/// <summary>
/// Office document operations API
/// </summary>
[ApiController]
[Route("api/office")]
public class OfficeController(OfficeService officeService, ILogger<OfficeController> logger) : ControllerBase
{
    /// <summary>
    /// Load an Office document into memory
    /// </summary>
    [HttpPost("load")]
    public async Task<IActionResult> LoadDocument([FromBody] LoadDocumentRequest request)
    {
        logger.LogInformation("Loading document: {FilePath}", request.FilePath);
        ServiceResult<LoadDocumentResult> result = await officeService.LoadDocumentAsync(request.FilePath, request.Password);
        return Ok(result);
    }

    /// <summary>
    /// Get list of all loaded Office documents
    /// </summary>
    [HttpGet("documents")]
    public IActionResult GetLoadedDocuments()
    {
        ServiceResult<LoadedDocumentsResult> result = officeService.GetLoadedDocuments();
        return Ok(result);
    }

    /// <summary>
    /// Extract all content from a loaded Office document
    /// </summary>
    [HttpGet("documents/content")]
    public async Task<IActionResult> ExtractAllContent([FromQuery] string filePath)
    {
        logger.LogInformation("Extracting all content from: {FilePath}", filePath);
        ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Search for text within a specific Office document
    /// </summary>
    [HttpPost("documents/search")]
    public async Task<IActionResult> SearchInDocument([FromQuery] string filePath, [FromBody] SearchRequest request)
    {
        logger.LogInformation("Searching for '{SearchTerm}' in: {FilePath}", request.SearchTerm, filePath);
        ServiceResult<SearchDocumentResult> result = await officeService.SearchInDocumentAsync(filePath, request.SearchTerm, request.FuzzySearch, request.MaxResults);
        return Ok(result);
    }

    /// <summary>
    /// Search across all loaded Office documents
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> SearchAcrossDocuments([FromBody] SearchRequest request)
    {
        logger.LogInformation("Searching across all documents for: {SearchTerm}", request.SearchTerm);
        ServiceResult<CrossDocumentSearchResult> result = await officeService.SearchAcrossDocumentsAsync(request.SearchTerm, request.FuzzySearch, request.MaxResults);
        return Ok(result);
    }

    /// <summary>
    /// Analyze document structure and content statistics
    /// </summary>
    [HttpGet("documents/analyze")]
    public async Task<IActionResult> AnalyzeDocument([FromQuery] string filePath)
    {
        logger.LogInformation("Analyzing document: {FilePath}", filePath);
        ServiceResult<DocumentAnalysisResult> result = await officeService.AnalyzeDocumentAsync(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Unload a specific Office document from memory
    /// </summary>
    [HttpDelete("documents/unload")]
    public IActionResult UnloadDocument([FromQuery] string filePath)
    {
        logger.LogInformation("Unloading document: {FilePath}", filePath);
        ServiceResult<SimpleOperationResult> result = officeService.UnloadDocument(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Clear all loaded Office documents from memory
    /// </summary>
    [HttpDelete("documents")]
    public IActionResult ClearAllDocuments()
    {
        logger.LogInformation("Clearing all documents");
        ServiceResult<SimpleOperationResult> result = officeService.ClearAllDocuments();
        return Ok(result);
    }

    /// <summary>
    /// Get service status and memory usage
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetServiceStatus()
    {
        ServiceResult<ServiceStatusInfo> result = officeService.GetServiceStatus();
        return Ok(result);
    }

    /// <summary>
    /// Extract data from Excel worksheets
    /// </summary>
    [HttpPost("documents/excel/extract")]
    public async Task<IActionResult> ExtractExcelData([FromQuery] string filePath, [FromBody] ExtractExcelRequest request)
    {
        logger.LogInformation("Extracting Excel data from: {FilePath}", filePath);
        ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Extract PowerPoint slides and speaker notes
    /// </summary>
    [HttpPost("documents/powerpoint/extract")]
    public async Task<IActionResult> ExtractPowerPointSlides([FromQuery] string filePath, [FromBody] ExtractPowerPointRequest request)
    {
        logger.LogInformation("Extracting PowerPoint slides from: {FilePath}", filePath);
        ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Extract Word document structure
    /// </summary>
    [HttpPost("documents/word/extract")]
    public async Task<IActionResult> ExtractWordStructure([FromQuery] string filePath, [FromBody] ExtractWordRequest request)
    {
        logger.LogInformation("Extracting Word structure from: {FilePath}", filePath);
        ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Compare two Office documents
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> CompareDocuments([FromBody] CompareDocumentsRequest request)
    {
        logger.LogInformation("Comparing documents: {FilePath1} vs {FilePath2}", request.FilePath1, request.FilePath2);
        ServiceResult<ExtractContentResult> result1 = await officeService.ExtractAllContentAsync(request.FilePath1);
        ServiceResult<ExtractContentResult> result2 = await officeService.ExtractAllContentAsync(request.FilePath2);
        
        var comparison = new
        {
            document1 = new { filePath = request.FilePath1, result = result1 },
            document2 = new { filePath = request.FilePath2, result = result2 },
            comparisonType = request.ComparisonType,
            message = "Document comparison feature - comparing extracted content"
        };
        
        return Ok(comparison);
    }

    /// <summary>
    /// Validate Office document integrity
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateDocument([FromBody] ValidateDocumentRequest request)
    {
        logger.LogInformation("Validating document: {FilePath}", request.FilePath);
        ServiceResult<LoadDocumentResult> result = await officeService.LoadDocumentAsync(request.FilePath);
        
        var validation = new
        {
            filePath = request.FilePath,
            isValid = result.Success,
            canOpen = result.Success,
            errorMessage = result.Success ? null : result.Error,
            validationChecks = new
            {
                fileExists = System.IO.File.Exists(request.FilePath),
                hasValidExtension = System.IO.Path.GetExtension(request.FilePath).ToLowerInvariant() is ".docx" or ".xlsx" or ".pptx" or ".doc" or ".xls" or ".ppt",
                fileSize = new System.IO.FileInfo(request.FilePath).Length,
                lastModified = new System.IO.FileInfo(request.FilePath).LastWriteTime
            }
        };
        
        return Ok(validation);
    }

    /// <summary>
    /// Get detailed metadata from an Office document
    /// </summary>
    [HttpGet("documents/metadata")]
    public async Task<IActionResult> GetDocumentMetadata([FromQuery] string filePath)
    {
        logger.LogInformation("Getting metadata from: {FilePath}", filePath);
        await officeService.LoadDocumentAsync(filePath);
        ServiceResult<DocumentAnalysisResult> result = await officeService.AnalyzeDocumentAsync(filePath);
        return Ok(result);
    }
}

// Request models
public record LoadDocumentRequest(string FilePath, string? Password = null);
public record SearchRequest(string SearchTerm, bool FuzzySearch = false, int MaxResults = 50);
public record ExtractExcelRequest(string? WorksheetName = null, string? CellRange = null, bool IncludeFormulas = true);
public record ExtractPowerPointRequest(int? SlideNumber = null, bool IncludeSpeakerNotes = true, bool IncludeImages = false);
public record ExtractWordRequest(bool IncludeTables = true, bool IncludeComments = true, bool IncludeHeadings = true);
public record CompareDocumentsRequest(string FilePath1, string FilePath2, string ComparisonType = "content");
public record ValidateDocumentRequest(string FilePath);
