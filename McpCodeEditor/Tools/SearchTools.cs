using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;
using McpCodeEditor.Models.Options;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools;

/// <summary>
/// Tools for advanced search operations across code and content.
/// </summary>
[McpServerToolType]
public class SearchTools(SearchService searchService) : BaseToolClass
{
    [McpServerTool]
    [Description("Build or rebuild the search index for enhanced searching")]
    public async Task<string> SearchRebuildIndexAsync()
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var success = await searchService.RebuildIndexAsync();
            
            return CreateSuccessResponse(new
            {
                index_rebuilt = success,
                timestamp = DateTime.Now
            }, success ? "Search index rebuilt successfully" : "Failed to rebuild search index");
        });
    }

    [McpServerTool]
    [Description("Search for symbols (classes, methods, properties) using semantic search")]
    public async Task<string> SearchSymbolsAsync(
        [Description("Symbol name to search for")]
        string query,
        [Description("Use fuzzy matching")]
        bool useFuzzyMatch = false,
        [Description("Fuzzy match threshold (0-100)")]
        int fuzzyThreshold = 70,
        [Description("Maximum number of results")]
        int maxResults = 50,
        [Description("Case sensitive search")]
        bool caseSensitive = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(query, nameof(query));

            var options = new SearchOptions
            {
                UseFuzzyMatch = useFuzzyMatch,
                FuzzyThreshold = fuzzyThreshold,
                MaxResults = maxResults,
                CaseSensitive = caseSensitive
            };

            var results = await searchService.SearchSymbolsAsync(query, options);

            return CreateSuccessResponse(new
            {
                query = query,
                options = new
                {
                    fuzzy_match = useFuzzyMatch,
                    fuzzy_threshold = fuzzyThreshold,
                    case_sensitive = caseSensitive,
                    max_results = maxResults
                },
                result_count = results.Count,
                results = results.Select(r => new
                {
                    symbol_name = r.SymbolName,
                    symbol_type = r.SymbolType,
                    file_path = r.FilePath,
                    line_number = r.LineNumber,
                    column = r.Column,
                    preview = r.Preview,
                    score = r.Score,
                    containing_type = r.Metadata.GetValueOrDefault("containingType", ""),
                    namespace_name = r.Metadata.GetValueOrDefault("namespace", "")
                }).ToArray()
            });
        });
    }

    [McpServerTool]
    [Description("Fuzzy search across all text content")]
    public async Task<string> SearchFuzzyAsync(
        [Description("Text to search for")]
        string query,
        [Description("Fuzzy match threshold (0-100)")]
        int threshold = 70,
        [Description("Maximum number of results")]
        int maxResults = 50,
        [Description("Include comments in search")]
        bool includeComments = true,
        [Description("Include strings in search")]
        bool includeStrings = true)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(query, nameof(query));

            var options = new SearchOptions
            {
                UseFuzzyMatch = true,
                FuzzyThreshold = threshold,
                MaxResults = maxResults,
                IncludeComments = includeComments,
                IncludeStrings = includeStrings
            };

            var results = await searchService.SearchTextAsync(query, options);

            return CreateSuccessResponse(new
            {
                query = query,
                threshold = threshold,
                options = new
                {
                    include_comments = includeComments,
                    include_strings = includeStrings,
                    max_results = maxResults
                },
                result_count = results.Count,
                results = results.Select(r => new
                {
                    file_path = r.FilePath,
                    line_number = r.LineNumber,
                    column = r.Column,
                    preview = r.Preview,
                    score = r.Score,
                    fuzzy_ratio = r.Metadata.GetValueOrDefault("fuzzyRatio", 0)
                }).ToArray()
            });
        });
    }
}
