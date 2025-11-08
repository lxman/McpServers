using System.ComponentModel;
using System.Text.Json;
using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.DocumentSearch;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DocumentMcp.McpTools;

/// <summary>
/// MCP tools for document search operations
/// </summary>
[McpServerToolType]
public class SearchTools(
    QuickSearchService searchService,
    ILogger<SearchTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("search_document")]
    [Description("Search within a single document. See skills/document/search/search-document.md only when using this tool")]
    public async Task<string> SearchDocument(
        string filePath,
        string searchTerm,
        bool fuzzySearch = false,
        int maxResults = 100)
    {
        try
        {
            logger.LogDebug("Searching in document: {FilePath}, Term: {SearchTerm}", filePath, searchTerm);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Search term is required" }, _jsonOptions);
            }

            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File not found" }, _jsonOptions);
            }

            var result = await searchService.SearchInDocumentAsync(filePath, searchTerm, fuzzySearch, maxResults, null);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                searchTerm,
                matchCount = result.Data?.MatchCount ?? 0,
                matches = result.Data?.Matches?.Select(m => new
                {
                    lineNumber = m.LineNumber,
                    position = m.Position,
                    context = m.Context,
                    matchedText = m.MatchedText,
                    fuzzyScore = m.FuzzyScore
                }),
                options = new
                {
                    fuzzySearch,
                    maxResults
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching document: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("search_all_documents")]
    [Description("Search across all loaded documents. See skills/document/search/search-all.md only when using this tool")]
    public async Task<string> SearchAllDocuments(
        string searchTerm,
        bool fuzzySearch = false,
        int maxResultsPerDocument = 10)
    {
        try
        {
            logger.LogDebug("Searching all documents for: {SearchTerm}", searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Search term is required" }, _jsonOptions);
            }

            var result = await searchService.SearchAcrossDocumentsAsync(searchTerm, fuzzySearch, maxResultsPerDocument);

            if (!result.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = result.Error }, _jsonOptions);
            }

            // Format results
            var documentResults = result.Data?
                .Select(searchResult => new
                {
                    filePath = searchResult.FilePath,
                    documentType = searchResult.DocumentType.ToString(),
                    matchCount = searchResult.MatchCount,
                    relevanceScore = searchResult.RelevanceScore,
                    matches = searchResult.Matches?.Select(m => new
                    {
                        lineNumber = m.LineNumber,
                        position = m.Position,
                        context = m.Context,
                        matchedText = m.MatchedText,
                        fuzzyScore = m.FuzzyScore
                    }).Take(maxResultsPerDocument)
                })
                .ToList();

            var totalMatches = result.Data?.Sum(r => r.MatchCount) ?? 0;

            return JsonSerializer.Serialize(new
            {
                success = true,
                searchTerm,
                totalMatches,
                documentsWithMatches = documentResults?.Count ?? 0,
                results = documentResults,
                options = new
                {
                    fuzzySearch,
                    maxResultsPerDocument
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching all documents");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}