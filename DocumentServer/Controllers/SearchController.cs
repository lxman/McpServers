using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Models.Requests;
using DocumentServer.Core.Services.DocumentSearch;
using Microsoft.AspNetCore.Mvc;

namespace DocumentServer.Controllers;

/// <summary>
/// Controller for quick in-memory search operations
/// </summary>
[ApiController]
[Route("api/search")]
public class SearchController(ILogger<SearchController> logger, QuickSearchService searchService)
    : ControllerBase
{
    /// <summary>
    /// Search within a specific loaded document
    /// </summary>
    /// <param name="request">Search parameters</param>
    /// <returns>Search results with matches</returns>
    [HttpPost("document")]
    [ProducesResponseType(typeof(ServiceResult<SearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchInDocument([FromBody] SearchInDocumentRequest request)
    {
        logger.LogInformation("Searching in document: {FilePath}, Query: {Query}", 
            request.FilePath, request.Query);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(ServiceResult<SearchResult>.CreateFailure("File path is required"));
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(ServiceResult<SearchResult>.CreateFailure("Search query is required"));
        }

        ServiceResult<SearchResult> result = await searchService.SearchInDocumentAsync(
            request.FilePath,
            request.Query,
            request.UseFuzzyMatching,
            request.MaxResults);

        if (result.Success)
        {
            logger.LogInformation("Search in document complete: {FilePath}, Found {Count} matches",
                request.FilePath, result.Data?.MatchCount ?? 0);
            return Ok(result);
        }

        logger.LogWarning("Search in document failed: {FilePath}, Error: {Error}",
            request.FilePath, result.Error);
        return BadRequest(result);
    }

    /// <summary>
    /// Search across all loaded documents
    /// </summary>
    /// <param name="request">Search parameters</param>
    /// <returns>Search results from all matching documents</returns>
    [HttpPost("all")]
    [ProducesResponseType(typeof(ServiceResult<List<SearchResult>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAcrossDocuments([FromBody] SearchAcrossDocumentsRequest request)
    {
        logger.LogInformation("Searching across all documents, Query: {Query}", request.Query);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(ServiceResult<List<SearchResult>>.CreateFailure("Search query is required"));
        }

        ServiceResult<List<SearchResult>> result = await searchService.SearchAcrossDocumentsAsync(
            request.Query,
            request.UseFuzzyMatching,
            request.MaxResultsPerDocument);

        if (result.Success)
        {
            int totalMatches = result.Data?.Sum(r => r.MatchCount) ?? 0;
            int documentCount = result.Data?.Count ?? 0;

            logger.LogInformation("Search across documents complete: Found {TotalMatches} matches in {DocumentCount} documents",
                totalMatches, documentCount);
            
            return Ok(result);
        }

        logger.LogWarning("Search across documents failed, Error: {Error}", result.Error);
        return BadRequest(result);
    }
}
