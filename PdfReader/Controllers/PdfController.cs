using Microsoft.AspNetCore.Mvc;
using PdfMcp.Models;
using PdfMcp.Services;

namespace PdfReader.Controllers;

/// <summary>
/// PDF document operations API
/// </summary>
[ApiController]
[Route("api/pdf")]
public class PdfController(PdfService pdfService, ILogger<PdfController> logger) : ControllerBase
{
    /// <summary>
    /// Load a PDF document into memory
    /// </summary>
    [HttpPost("load")]
    public async Task<IActionResult> LoadPdf([FromBody] LoadPdfRequest request)
    {
        logger.LogInformation("Loading PDF: {FilePath}", request.FilePath);
        ServiceResult<LoadPdfResult> result = await pdfService.LoadPdfAsync(request.FilePath, request.Password);
        return Ok(result);
    }

    /// <summary>
    /// Get list of all loaded PDF documents
    /// </summary>
    [HttpGet("documents")]
    public IActionResult GetLoadedDocuments()
    {
        ServiceResult<LoadedDocumentsResult> result = pdfService.GetLoadedDocuments();
        return Ok(result);
    }

    /// <summary>
    /// Get comprehensive information about a specific PDF document
    /// </summary>
    [HttpGet("documents/info")]
    public IActionResult GetDocumentInfo([FromQuery] string filePath)
    {
        logger.LogInformation("Getting document info: {FilePath}", filePath);
        ServiceResult<DocumentInfoResult> result = pdfService.GetDocumentInfo(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Get content of a specific page
    /// </summary>
    [HttpGet("documents/pages")]
    public IActionResult GetPageContent([FromQuery] string filePath, [FromQuery] int pageNumber)
    {
        logger.LogInformation("Getting page {PageNumber} content from: {FilePath}", pageNumber, filePath);
        ServiceResult<PageContentResult> result = pdfService.GetPageContent(filePath, pageNumber);
        return Ok(result);
    }

    /// <summary>
    /// Search for text within a PDF document
    /// </summary>
    [HttpPost("documents/search")]
    public IActionResult SearchInDocument([FromQuery] string filePath, [FromBody] SearchRequest request)
    {
        logger.LogInformation("Searching for '{SearchTerm}' in: {FilePath}", request.SearchTerm, filePath);
        ServiceResult<SearchInDocumentResult> result = pdfService.SearchInDocument(filePath, request.SearchTerm, request.FuzzySearch, request.MaxResults);
        return Ok(result);
    }

    /// <summary>
    /// Generate a summary of the PDF document
    /// </summary>
    [HttpPost("documents/summarize")]
    public IActionResult SummarizeDocument([FromQuery] string filePath, [FromBody] SummarizeRequest request)
    {
        logger.LogInformation("Summarizing document: {FilePath}", filePath);
        ServiceResult<DocumentSummary> result = pdfService.SummarizeDocument(filePath, request.MaxLength);
        return Ok(result);
    }

    /// <summary>
    /// Extract all images from a PDF document
    /// </summary>
    [HttpPost("documents/extract-images")]
    public IActionResult ExtractImages([FromQuery] string filePath, [FromBody] ExtractImagesRequest request)
    {
        logger.LogInformation("Extracting images from: {FilePath}", filePath);
        ServiceResult<ExtractImagesResult> result = pdfService.ExtractImages(filePath, request.OutputDirectory);
        return Ok(result);
    }

    /// <summary>
    /// Validate a PDF file for integrity and corruption
    /// </summary>
    [HttpPost("validate")]
    public IActionResult ValidatePdf([FromBody] ValidatePdfRequest request)
    {
        logger.LogInformation("Validating PDF: {FilePath}", request.FilePath);
        ServiceResult<PdfValidationResult> result = pdfService.ValidatePdf(request.FilePath);
        return Ok(result);
    }

    /// <summary>
    /// Extract all text from a PDF document
    /// </summary>
    [HttpGet("documents/text")]
    public IActionResult ExtractAllText([FromQuery] string filePath)
    {
        logger.LogInformation("Extracting all text from: {FilePath}", filePath);
        ServiceResult<TextExtractionResult> result = pdfService.ExtractAllTextFromDocument(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Get metadata from a PDF document
    /// </summary>
    [HttpGet("documents/metadata")]
    public IActionResult GetDocumentMetadata([FromQuery] string filePath)
    {
        logger.LogInformation("Getting metadata from: {FilePath}", filePath);
        ServiceResult<DocumentMetadataResult> result = pdfService.GetDocumentMetadata(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Search across all loaded PDF documents
    /// </summary>
    [HttpPost("search")]
    public IActionResult SearchAcrossDocuments([FromBody] SearchRequest request)
    {
        logger.LogInformation("Searching across all documents for: {SearchTerm}", request.SearchTerm);
        ServiceResult<CrossDocumentSearchResult> result = pdfService.SearchAcrossAllDocuments(request.SearchTerm, request.FuzzySearch, request.MaxResults);
        return Ok(result);
    }

    /// <summary>
    /// Compare two PDF documents
    /// </summary>
    [HttpPost("compare")]
    public IActionResult CompareDocuments([FromBody] CompareDocumentsRequest request)
    {
        logger.LogInformation("Comparing documents: {FilePath1} vs {FilePath2}", request.FilePath1, request.FilePath2);
        ServiceResult<DocumentComparisonResult> result = pdfService.CompareDocuments(request.FilePath1, request.FilePath2);
        return Ok(result);
    }

    /// <summary>
    /// Analyze document structure
    /// </summary>
    [HttpGet("documents/analyze")]
    public IActionResult AnalyzeDocumentStructure([FromQuery] string filePath)
    {
        logger.LogInformation("Analyzing structure of: {FilePath}", filePath);
        ServiceResult<DocumentStructureAnalysis> result = pdfService.AnalyzeDocumentStructure(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Unload a specific PDF document from memory
    /// </summary>
    [HttpDelete("documents/unload")]
    public IActionResult UnloadDocument([FromQuery] string filePath)
    {
        logger.LogInformation("Unloading document: {FilePath}", filePath);
        ServiceResult<SimpleOperationResult> result = pdfService.UnloadDocument(filePath);
        return Ok(result);
    }

    /// <summary>
    /// Clear all loaded PDF documents from memory
    /// </summary>
    [HttpDelete("documents")]
    public IActionResult ClearAllDocuments()
    {
        logger.LogInformation("Clearing all documents");
        ServiceResult<SimpleOperationResult> result = pdfService.ClearAllDocuments();
        return Ok(result);
    }

    /// <summary>
    /// Get service status and memory usage
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetServiceStatus()
    {
        ServiceResult<ServiceStatusInfo> result = pdfService.GetServiceStatus();
        return Ok(result);
    }
}

// Request models
public record LoadPdfRequest(string FilePath, string? Password = null);
public record SearchRequest(string SearchTerm, bool FuzzySearch = false, int MaxResults = 50);
public record SummarizeRequest(int MaxLength = 500);
public record ExtractImagesRequest(string OutputDirectory);
public record ValidatePdfRequest(string FilePath);
public record CompareDocumentsRequest(string FilePath1, string FilePath2);