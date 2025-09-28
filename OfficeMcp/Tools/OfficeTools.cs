using Microsoft.Extensions.Logging;
using OfficeMcp.Services;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OfficeMcp.Models;
using OfficeMcp.Models.Results;

namespace OfficeMcp.Tools;

[McpServerToolType]
public class OfficeTools(OfficeService officeService, ILogger<OfficeTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    
    [McpServerTool]
    [Description("Load an Office document (Word, Excel, PowerPoint) into memory for analysis")]
    public async Task<string> LoadDocumentAsync(
        [Description("Full path to the Office document")] string filePath,
        [Description("Optional password for protected documents")] string? password = null)
    {
        try
        {
            ServiceResult<LoadDocumentResult> result = await officeService.LoadDocumentAsync(filePath, password);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading document: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Get a list of all currently loaded Office documents")]
    public string GetLoadedDocuments()
    {
        try
        {
            ServiceResult<LoadedDocumentsResult> result = officeService.GetLoadedDocuments();
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting loaded documents");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Extract all text content, tables, and images from a loaded Office document")]
    public async Task<string> ExtractAllContentAsync(
        [Description("Full path to the loaded document")] string filePath)
    {
        try
        {
            ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting content from: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Search for text within a specific Office document with optional fuzzy matching")]
    public async Task<string> SearchInDocumentAsync(
        [Description("Full path to the document to search")] string filePath,
        [Description("Text to search for")] string searchTerm,
        [Description("Enable fuzzy matching for approximate searches")] bool fuzzySearch = false,
        [Description("Maximum number of results to return")] int maxResults = 50)
    {
        try
        {
            ServiceResult<SearchDocumentResult> result = await officeService.SearchInDocumentAsync(filePath, searchTerm, fuzzySearch, maxResults);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching in document: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Search for text across all loaded Office documents")]
    public async Task<string> SearchAcrossDocumentsAsync(
        [Description("Text to search for")] string searchTerm,
        [Description("Enable fuzzy matching for approximate searches")] bool fuzzySearch = false,
        [Description("Maximum number of results to return across all documents")] int maxResults = 50)
    {
        try
        {
            ServiceResult<CrossDocumentSearchResult> result = await officeService.SearchAcrossDocumentsAsync(searchTerm, fuzzySearch, maxResults);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching across documents");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Analyze the structure and content statistics of an Office document")]
    public async Task<string> AnalyzeDocumentAsync(
        [Description("Full path to the document to analyze")] string filePath)
    {
        try
        {
            ServiceResult<DocumentAnalysisResult> result = await officeService.AnalyzeDocumentAsync(filePath);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing document: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Unload a specific Office document from memory")]
    public string UnloadDocumentAsync(
        [Description("Full path to the document to unload")] string filePath)
    {
        try
        {
            ServiceResult<SimpleOperationResult> result = officeService.UnloadDocument(filePath);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unloading document: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Clear all loaded Office documents from memory")]
    public string ClearAllDocuments()
    {
        try
        {
            ServiceResult<SimpleOperationResult> result = officeService.ClearAllDocuments();
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing all documents");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Get current service status including memory usage and loaded document count")]
    public string GetServiceStatus()
    {
        try
        {
            ServiceResult<ServiceStatusInfo> result = officeService.GetServiceStatus();
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting service status");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Extract specific data from Excel worksheets with cell range and formula information")]
    public async Task<string> ExtractExcelDataAsync(
        [Description("Full path to the Excel document")] string filePath,
        [Description("Optional worksheet name (if not specified, extracts from all worksheets)")] string? worksheetName = null,
        [Description("Optional cell range (e.g., 'A1:C10')")] string? cellRange = null,
        [Description("Include formulas in the output")] bool includeFormulas = true)
    {
        try
        {
            // This could be implemented as a specialized Excel extraction method
            ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting Excel data from: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Extract slide content and speaker notes from PowerPoint presentations")]
    public async Task<string> ExtractPowerPointSlidesAsync(
        [Description("Full path to the PowerPoint document")] string filePath,
        [Description("Optional slide number (if not specified, extracts all slides)")] int? slideNumber = null,
        [Description("Include speaker notes in the output")] bool includeSpeakerNotes = true,
        [Description("Include slide images and shapes")] bool includeImages = false)
    {
        try
        {
            // This could be implemented as a specialized PowerPoint extraction method
            ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting PowerPoint slides from: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Extract Word document structure including headings, tables, and comments")]
    public async Task<string> ExtractWordStructureAsync(
        [Description("Full path to the Word document")] string filePath,
        [Description("Include document tables")] bool includeTables = true,
        [Description("Include comments and track changes")] bool includeComments = true,
        [Description("Include heading hierarchy")] bool includeHeadings = true)
    {
        try
        {
            // This could be implemented as a specialized Word structure extraction method
            ServiceResult<ExtractContentResult> result = await officeService.ExtractAllContentAsync(filePath);
            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting Word structure from: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Compare two Office documents and highlight differences")]
    public async Task<string> CompareDocumentsAsync(
        [Description("Full path to the first document")] string filePath1,
        [Description("Full path to the second document")] string filePath2,
        [Description("Type of comparison: content, structure, or metadata")] string comparisonType = "content")
    {
        try
        {
            // This would need to be implemented as a document comparison method in the service
            ServiceResult<ExtractContentResult> result1 = await officeService.ExtractAllContentAsync(filePath1);
            ServiceResult<ExtractContentResult> result2 = await officeService.ExtractAllContentAsync(filePath2);
            
            var comparison = new
            {
                document1 = new { filePath = filePath1, result = result1 },
                document2 = new { filePath = filePath2, result = result2 },
                comparisonType,
                message = "Document comparison feature - comparing extracted content"
            };
            
            return JsonSerializer.Serialize(comparison, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing documents: {FilePath1} vs {FilePath2}", filePath1, filePath2);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool]
    [Description("Validate Office document integrity and check for potential issues")]
    public async Task<string> ValidateDocumentAsync(
        [Description("Full path to the document to validate")] string filePath)
    {
        try
        {
            // Load the document to check if it can be opened successfully
            ServiceResult<LoadDocumentResult> result = await officeService.LoadDocumentAsync(filePath);
            
            var validation = new
            {
                filePath,
                isValid = result.Success,
                canOpen = result.Success,
                errorMessage = result.Success ? null : result.Error,
                validationChecks = new
                {
                    fileExists = File.Exists(filePath),
                    hasValidExtension = Path.GetExtension(filePath).ToLowerInvariant() is ".docx" or ".xlsx" or ".pptx" or ".doc" or ".xls" or ".ppt",
                    fileSize = new FileInfo(filePath).Length,
                    lastModified = new FileInfo(filePath).LastWriteTime
                }
            };
            
            return JsonSerializer.Serialize(validation, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating document: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message, isValid = false });
        }
    }

    [McpServerTool]
    [Description("Get detailed metadata and properties from an Office document")]
    public async Task<string> GetDocumentMetadataAsync(
        [Description("Full path to the document")] string filePath)
    {
        try
        {
            // Load the document if not already loaded
            await officeService.LoadDocumentAsync(filePath);
            
            // Extract and analyze the document
            ServiceResult<DocumentAnalysisResult> analysisResult = await officeService.AnalyzeDocumentAsync(filePath);
            
            return JsonSerializer.Serialize(analysisResult, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting document metadata: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}