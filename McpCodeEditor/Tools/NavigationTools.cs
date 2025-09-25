using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;
using McpCodeEditor.Models;
using McpCodeEditor.Tools.Common;
using Microsoft.CodeAnalysis;

namespace McpCodeEditor.Tools;

/// <summary>
/// Tools for symbol navigation and code exploration operations.
/// </summary>
[McpServerToolType]
public class NavigationTools(SymbolNavigationService symbolNavigationService) : BaseToolClass
{
    [McpServerTool]
    [Description("Go to the definition of a symbol at the specified location")]
    public async Task<string> NavigateGoToDefinitionAsync(
        [Description("Path to the file")]
        string filePath,
        [Description("Line number (1-based)")]
        int lineNumber,
        [Description("Column number (1-based)")]
        int column)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateLineNumber(lineNumber);

            if (column <= 0)
                throw new ArgumentException("Column must be positive (1-based column numbers).", nameof(column));

            var result = await symbolNavigationService.GoToDefinitionAsync(filePath, lineNumber, column);

            return CreateSuccessResponse(new
            {
                success = result.Success,
                message = result.Message,
                error = result.Error,
                searched_location = new
                {
                    file_path = filePath,
                    line = lineNumber,
                    column = column
                },
                symbol = result.Symbol != null ? new
                {
                    name = result.Symbol.Name,
                    full_name = result.Symbol.FullName,
                    kind = result.Symbol.Kind,
                    containing_type = result.Symbol.ContainingType,
                    namespace_name = result.Symbol.Namespace,
                    is_from_source = result.Symbol.IsFromSource,
                    properties = result.Symbol.Properties
                } : null,
                definition_count = result.Locations.Count,
                definitions = result.Locations.Select(l => new
                {
                    file_path = l.FilePath,
                    line_number = l.LineNumber,
                    column = l.Column,
                    preview = l.Preview,
                    location_type = l.LocationType,
                    context = l.Context
                }).ToArray(),
                metadata = result.Metadata
            });
        });
    }

    [McpServerTool]
    [Description("Find all references to a symbol at the specified location")]
    public async Task<string> NavigateFindReferencesAsync(
        [Description("Path to the file")]
        string filePath,
        [Description("Line number (1-based)")]
        int lineNumber,
        [Description("Column number (1-based)")]
        int column,
        [Description("Include symbol definitions")]
        bool includeDefinitions = true,
        [Description("Include symbol references")]
        bool includeReferences = true,
        [Description("Include implementations (for interfaces/virtual members)")]
        bool includeImplementations = false,
        [Description("Maximum number of results")]
        int maxResults = 500)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateLineNumber(lineNumber);

            if (column <= 0)
                throw new ArgumentException("Column must be positive (1-based column numbers).", nameof(column));

            if (maxResults <= 0)
                throw new ArgumentException("MaxResults must be positive.", nameof(maxResults));

            var options = new ReferenceSearchOptions
            {
                IncludeDefinitions = includeDefinitions,
                IncludeReferences = includeReferences,
                IncludeImplementations = includeImplementations,
                MaxResults = maxResults
            };

            var result = await symbolNavigationService.FindReferencesAsync(filePath, lineNumber, column, options);

            return CreateSuccessResponse(new
            {
                success = result.Success,
                message = result.Message,
                error = result.Error,
                searched_location = new
                {
                    file_path = filePath,
                    line = lineNumber,
                    column = column
                },
                symbol = result.Symbol != null ? new
                {
                    name = result.Symbol.Name,
                    full_name = result.Symbol.FullName,
                    kind = result.Symbol.Kind,
                    containing_type = result.Symbol.ContainingType,
                    namespace_name = result.Symbol.Namespace
                } : null,
                reference_count = result.Locations.Count,
                references = result.Locations.Select(l => new
                {
                    file_path = l.FilePath,
                    line_number = l.LineNumber,
                    column = l.Column,
                    preview = l.Preview,
                    location_type = l.LocationType,
                    context = l.Context
                }).ToArray(),
                search_options = new
                {
                    include_definitions = includeDefinitions,
                    include_references = includeReferences,
                    include_implementations = includeImplementations,
                    max_results = maxResults
                },
                metadata = result.Metadata
            });
        });
    }

    [McpServerTool]
    [Description("Find symbols by name across the entire workspace")]
    public async Task<string> NavigateFindSymbolsByNameAsync(
        [Description("Symbol name to search for")]
        string symbolName,
        [Description("Exact match (true) or partial match (false)")]
        bool exactMatch = false,
        [Description("Symbol kind filter (Method, Class, Property, Field, etc.)")]
        string? symbolKind = null,
        [Description("Maximum number of results")]
        int maxResults = 100)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(symbolName, nameof(symbolName));

            if (maxResults <= 0)
                throw new ArgumentException("MaxResults must be positive.", nameof(maxResults));

            SymbolKind? kind = null;
            if (!string.IsNullOrEmpty(symbolKind) &&
                Enum.TryParse<SymbolKind>(symbolKind, true, out var parsedKind))
            {
                kind = parsedKind;
            }

            var result = await symbolNavigationService.FindSymbolsByNameAsync(
                symbolName, exactMatch, kind, maxResults);

            return CreateSuccessResponse(new
            {
                success = result.Success,
                message = result.Message,
                error = result.Error,
                search_query = symbolName,
                exact_match = exactMatch,
                symbol_kind_filter = symbolKind,
                symbol_count = result.Locations.Count,
                symbols = result.Locations.Select(l => new
                {
                    file_path = l.FilePath,
                    line_number = l.LineNumber,
                    column = l.Column,
                    preview = l.Preview,
                    location_type = l.LocationType,
                    context = l.Context
                }).ToArray(),
                metadata = result.Metadata
            });
        });
    }

    [McpServerTool]
    [Description("Refresh the symbol navigation workspace")]
    public async Task<string> NavigateRefreshWorkspaceAsync()
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var success = await symbolNavigationService.RefreshWorkspaceAsync();
            
            return CreateSuccessResponse(new
            {
                workspace_refreshed = success,
                timestamp = DateTime.Now
            }, success ? "Symbol navigation workspace refreshed successfully" : "Failed to refresh workspace");
        });
    }
}
