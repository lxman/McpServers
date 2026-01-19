using System.ComponentModel;
using System.Text.Json;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CodeAssistMcp.McpTools;

/// <summary>
/// MCP tools for semantic code search.
/// </summary>
[McpServerToolType]
public class SearchTools(
    RepositoryIndexer indexer,
    ILogger<SearchTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("search_code")]
    [Description("Semantic search across an indexed repository. Returns code chunks most similar to the query, with file paths, line numbers, and relevance scores. Use natural language queries like 'function that handles user authentication' or 'error handling for database connections'.")]
    public async Task<string> SearchCode(
        string repositoryName,
        string query,
        int limit = 10,
        float minScore = 0.5f)
    {
        try
        {
            logger.LogDebug("Searching {Repository} for: {Query}", repositoryName, query);

            var response = await indexer.SearchAsync(repositoryName, query, limit, minScore);

            var results = response.Results.Select(r => new
            {
                filePath = r.Chunk.RelativePath,
                startLine = r.Chunk.StartLine,
                endLine = r.Chunk.EndLine,
                chunkType = r.Chunk.ChunkType,
                symbolName = r.Chunk.SymbolName,
                parentSymbol = r.Chunk.ParentSymbol,
                language = r.Chunk.Language,
                score = Math.Round(r.Score, 4),
                content = r.Chunk.Content
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                query,
                repositoryName,
                resultCount = results.Count,
                duration = response.Duration.ToString(),
                results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching {Repository} for: {Query}", repositoryName, query);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("find_similar_code")]
    [Description("Find code similar to a given code snippet. Useful for finding duplicates, related implementations, or understanding patterns used elsewhere in the codebase.")]
    public async Task<string> FindSimilarCode(
        string repositoryName,
        string codeSnippet,
        int limit = 5,
        float minScore = 0.6f)
    {
        try
        {
            logger.LogDebug("Finding similar code in {Repository}", repositoryName);

            var response = await indexer.SearchAsync(repositoryName, codeSnippet, limit, minScore);

            var results = response.Results.Select(r => new
            {
                filePath = r.Chunk.RelativePath,
                startLine = r.Chunk.StartLine,
                endLine = r.Chunk.EndLine,
                chunkType = r.Chunk.ChunkType,
                symbolName = r.Chunk.SymbolName,
                parentSymbol = r.Chunk.ParentSymbol,
                language = r.Chunk.Language,
                score = Math.Round(r.Score, 4),
                content = r.Chunk.Content
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                resultCount = results.Count,
                duration = response.Duration.ToString(),
                results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding similar code in {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("search_by_symbol")]
    [Description("Search for code by symbol name (class, method, property, etc.). Combines semantic search with the symbol name for more targeted results.")]
    public async Task<string> SearchBySymbol(
        string repositoryName,
        string symbolName,
        string? symbolType = null,
        int limit = 10)
    {
        try
        {
            // Build a query that emphasizes the symbol
            var query = symbolType != null
                ? $"{symbolType} named {symbolName}"
                : $"symbol named {symbolName}";

            logger.LogDebug("Searching {Repository} for symbol: {Symbol}", repositoryName, symbolName);

            var response = await indexer.SearchAsync(repositoryName, query, limit * 2, 0.3f);

            // Filter results to those containing the symbol name
            var results = response.Results
                .Where(r => r.Chunk.SymbolName?.Contains(symbolName, StringComparison.OrdinalIgnoreCase) == true ||
                           r.Chunk.Content.Contains(symbolName, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .Select(r => new
                {
                    filePath = r.Chunk.RelativePath,
                    startLine = r.Chunk.StartLine,
                    endLine = r.Chunk.EndLine,
                    chunkType = r.Chunk.ChunkType,
                    symbolName = r.Chunk.SymbolName,
                    parentSymbol = r.Chunk.ParentSymbol,
                    language = r.Chunk.Language,
                    score = Math.Round(r.Score, 4),
                    content = r.Chunk.Content
                }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                searchedSymbol = symbolName,
                symbolType,
                resultCount = results.Count,
                duration = response.Duration.ToString(),
                results
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching for symbol {Symbol} in {Repository}", symbolName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("explain_code_area")]
    [Description("Get code from a specific area of the repository to understand its purpose. Searches for code related to a concept and returns surrounding context.")]
    public async Task<string> ExplainCodeArea(
        string repositoryName,
        string concept,
        int limit = 5)
    {
        try
        {
            logger.LogDebug("Explaining code area for '{Concept}' in {Repository}", concept, repositoryName);

            var response = await indexer.SearchAsync(repositoryName, concept, limit, 0.4f);

            var areas = response.Results.Select(r => new
            {
                filePath = r.Chunk.RelativePath,
                location = $"Lines {r.Chunk.StartLine}-{r.Chunk.EndLine}",
                chunkType = r.Chunk.ChunkType,
                symbolName = r.Chunk.SymbolName,
                parentSymbol = r.Chunk.ParentSymbol,
                relevanceScore = Math.Round(r.Score, 4),
                code = r.Chunk.Content
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                concept,
                repositoryName,
                areasFound = areas.Count,
                duration = response.Duration.ToString(),
                codeAreas = areas
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error explaining code area for '{Concept}' in {Repository}", concept, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
