using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PdfMcp.Models;
using PdfMcp.Services;

namespace PdfMcp.Tools;

[McpServerToolType]
public class PdfTools(PdfService pdfService)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool]
    [Description("Load a PDF document into memory for analysis. Supports password-protected PDFs.")]
    public async Task<string> LoadPdfAsync(
        [Description("Full path to the PDF file - must be canonical")]
        string filePath,
        [Description("Optional password for encrypted PDFs")]
        string? password = null)
    {
        ServiceResult<LoadPdfResult> result = await pdfService.LoadPdfAsync(filePath, password);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Get a list of all currently loaded PDF documents with basic information.")]
    public string GetLoadedDocuments()
    {
        ServiceResult<LoadedDocumentsResult> result = pdfService.GetLoadedDocuments();
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Get comprehensive information about a specific loaded PDF document including metadata, structure, and properties.")]
    public string GetDocumentInfo(
        [Description("Full path to the loaded PDF document")]
        string filePath)
    {
        ServiceResult<DocumentInfoResult> result = pdfService.GetDocumentInfo(filePath);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Get the complete content of a specific page including text, images, annotations, and links.")]
    public string GetPageContent(
        [Description("Full path to the loaded PDF document")]
        string filePath,
        [Description("Page number to retrieve (1-based)")]
        int pageNumber)
    {
        ServiceResult<PageContentResult> result = pdfService.GetPageContent(filePath, pageNumber);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Search for text within a loaded PDF document with support for exact and fuzzy matching.")]
    public string SearchInDocument(
        [Description("Full path to the loaded PDF document")]
        string filePath,
        [Description("Text to search for")]
        string searchTerm,
        [Description("Enable fuzzy matching for approximate searches (default: false)")]
        bool fuzzySearch = false,
        [Description("Maximum number of results to return (default: 50)")]
        int maxResults = 50)
    {
        ServiceResult<SearchInDocumentResult> result = pdfService.SearchInDocument(filePath, searchTerm, fuzzySearch, maxResults);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Generate an intelligent summary of the PDF document with key points and topics.")]
    public string SummarizeDocument(
        [Description("Full path to the loaded PDF document")]
        string filePath,
        [Description("Maximum length of the summary in characters (default: 500)")]
        int maxLength = 500)
    {
        ServiceResult<DocumentSummary> result = pdfService.SummarizeDocument(filePath, maxLength);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Extract all images from a PDF document and save them to a specified directory.")]
    public string ExtractImages(
        [Description("Full path to the loaded PDF document")]
        string filePath,
        [Description("Directory path where extracted images will be saved - must be canonical")]
        string outputDirectory)
    {
        ServiceResult<ExtractImagesResult> result = pdfService.ExtractImages(filePath, outputDirectory);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Validate a PDF file for integrity, corruption, and structural issues.")]
    public string ValidatePdf(
        [Description("Full path to the PDF file to validate - must be canonical")]
        string filePath)
    {
        ServiceResult<PdfValidationResult> result = pdfService.ValidatePdf(filePath);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Extract the complete text content from all pages of a PDF document.")]
    public string ExtractAllText(
        [Description("Full path to the loaded PDF document")]
        string filePath)
    {
        // This should be a service method that handles the logic internally
        ServiceResult<TextExtractionResult> result = pdfService.ExtractAllTextFromDocument(filePath);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Get detailed metadata information from a PDF document including creation date, author, security settings, etc.")]
    public string GetDocumentMetadata(
        [Description("Full path to the loaded PDF document")]
        string filePath)
    {
        // This should be a dedicated service method that returns structured metadata
        ServiceResult<DocumentMetadataResult> result = pdfService.GetDocumentMetadata(filePath);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Search for text across multiple loaded PDF documents simultaneously.")]
    public string SearchAcrossDocuments(
        [Description("Text to search for")]
        string searchTerm,
        [Description("Enable fuzzy matching for approximate searches (default: false)")]
        bool fuzzySearch = false,
        [Description("Maximum number of results per document (default: 10)")]
        int maxResultsPerDocument = 10)
    {
        // This complex operation should be handled entirely by the service
        ServiceResult<CrossDocumentSearchResult> result = pdfService.SearchAcrossAllDocuments(searchTerm, fuzzySearch, maxResultsPerDocument);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Compare two PDF documents by analyzing their structure, content, and differences.")]
    public string CompareDocuments(
        [Description("Full path to the first PDF document")]
        string filePath1,
        [Description("Full path to the second PDF document")]
        string filePath2)
    {
        // Document comparison logic should be in the service layer
        ServiceResult<DocumentComparisonResult> result = pdfService.CompareDocuments(filePath1, filePath2);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Get statistics and analysis about the structure and content of a PDF document.")]
    public string AnalyzeDocumentStructure(
        [Description("Full path to the loaded PDF document")]
        string filePath)
    {
        // Complex document analysis should be a service operation
        ServiceResult<DocumentStructureAnalysis> result = pdfService.AnalyzeDocumentStructure(filePath);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Unload a specific PDF document from memory to free up resources.")]
    public string UnloadDocument(
        [Description("Full path to the PDF document to unload")]
        string filePath)
    {
        ServiceResult<SimpleOperationResult> result = pdfService.UnloadDocument(filePath);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Clear all loaded PDF documents from memory.")]
    public string ClearAllDocuments()
    {
        ServiceResult<SimpleOperationResult> result = pdfService.ClearAllDocuments();
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool]
    [Description("Get memory usage and performance statistics for the PDF service.")]
    public string GetServiceStatus()
    {
        // Service status should be calculated by the service itself
        ServiceResult<ServiceStatusInfo> result = pdfService.GetServiceStatus();
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}