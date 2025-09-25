using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Models;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services;

public class SymbolNavigationService : IDisposable
{
    private readonly IWorkspaceManagementService _workspaceManagement;
    private readonly IDocumentManagementService _documentManagement;
    private readonly ISemanticAnalysisService _semanticAnalysis;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SymbolNavigationService>? _logger;

    public SymbolNavigationService(
        IWorkspaceManagementService workspaceManagement,
        IDocumentManagementService documentManagement,
        ISemanticAnalysisService semanticAnalysis,
        IMemoryCache cache,
        ILogger<SymbolNavigationService>? logger = null)
    {
        _workspaceManagement = workspaceManagement;
        _documentManagement = documentManagement;
        _semanticAnalysis = semanticAnalysis;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Initialize or refresh the workspace for symbol navigation
    /// Delegates to the workspace management service
    /// </summary>
    public async Task<bool> RefreshWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        return await _workspaceManagement.RefreshWorkspaceAsync(cancellationToken);
    }

    /// <summary>
    /// Find the definition of a symbol at the specified location
    /// </summary>
    public async Task<SymbolNavigationResult> GoToDefinitionAsync(
        string filePath,
        int lineNumber,
        int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentSolution = _workspaceManagement.CurrentSolution;
            if (currentSolution == null)
            {
                var refreshed = await _workspaceManagement.RefreshWorkspaceAsync(cancellationToken);
                currentSolution = _workspaceManagement.CurrentSolution;
                if (!refreshed || currentSolution == null)
                {
                    return new SymbolNavigationResult
                    {
                        Success = false,
                        Error = "Unable to load workspace for symbol navigation. The developer environment could not be initialized properly."
                    };
                }
            }

            var document = await _documentManagement.ResolveDocumentAsync(filePath, cancellationToken);
            if (document == null)
            {
                return new SymbolNavigationResult
                {
                    Success = false,
                    Error = $"Document not found in workspace: {filePath}"
                };
            }

            var sourceText = await document.GetTextAsync(cancellationToken);
            var position = GetPosition(sourceText, lineNumber, column);

            if (position < 0 || position >= sourceText.Length)
            {
                return new SymbolNavigationResult
                {
                    Success = false,
                    Error = "Invalid position specified"
                };
            }

            // Use semantic analysis service to find symbol at position
            var symbolAnalysis = await _semanticAnalysis.AnalyzeSymbolAtPositionAsync(document, position, cancellationToken);
            if (symbolAnalysis == null)
            {
                return new SymbolNavigationResult
                {
                    Success = false,
                    Error = "No symbol found at the specified position"
                };
            }

            var symbol = symbolAnalysis.Symbol;
            var locations = new List<SymbolLocation>();

            // Add definition locations
            foreach (var location in symbol.Locations)
            {
                if (location.IsInSource)
                {
                    var symbolLocation = await CreateSymbolLocationAsync(location, "Definition", cancellationToken);
                    if (symbolLocation != null)
                    {
                        locations.Add(symbolLocation);
                    }
                }
            }

            // If no source locations, try to find the original definition
            if (locations.Count == 0 && symbol.OriginalDefinition != null)
            {
                foreach (var location in symbol.OriginalDefinition.Locations)
                {
                    if (location.IsInSource)
                    {
                        var symbolLocation = await CreateSymbolLocationAsync(location, "Definition", cancellationToken);
                        if (symbolLocation != null)
                        {
                            locations.Add(symbolLocation);
                        }
                    }
                }
            }

            var result = new SymbolNavigationResult
            {
                Success = true,
                Message = locations.Count != 0
                    ? $"Found {locations.Count} definition(s) for '{symbol.Name}'"
                    : $"Symbol '{symbol.Name}' is defined in metadata (external assembly)",
                Locations = locations,
                Symbol = symbolAnalysis.NavigationInfo,
                Metadata =
                {
                    ["searchedPosition"] = $"{filePath}:{lineNumber}:{column}",
                    ["symbolKind"] = symbol.Kind.ToString(),
                    ["workspaceMode"] = _workspaceManagement.IsUsingFallbackWorkspace ? "Fallback" : "MSBuild",
                    ["environmentInitialized"] = _workspaceManagement.IsEnvironmentInitialized
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            return new SymbolNavigationResult
            {
                Success = false,
                Error = $"Go to definition failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Find all references to a symbol at the specified location
    /// </summary>
    public async Task<SymbolNavigationResult> FindReferencesAsync(
        string filePath,
        int lineNumber,
        int column,
        ReferenceSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ReferenceSearchOptions();

        try
        {
            var currentSolution = _workspaceManagement.CurrentSolution;
            if (currentSolution == null)
            {
                var refreshed = await _workspaceManagement.RefreshWorkspaceAsync(cancellationToken);
                currentSolution = _workspaceManagement.CurrentSolution;
                if (!refreshed || currentSolution == null)
                {
                    return new SymbolNavigationResult
                    {
                        Success = false,
                        Error = "Unable to load workspace for symbol navigation. The developer environment could not be initialized properly."
                    };
                }
            }

            var document = await _documentManagement.ResolveDocumentAsync(filePath, cancellationToken);
            if (document == null)
            {
                return new SymbolNavigationResult
                {
                    Success = false,
                    Error = $"Document not found in workspace: {filePath}"
                };
            }

            var sourceText = await document.GetTextAsync(cancellationToken);
            var position = GetPosition(sourceText, lineNumber, column);

            // Use semantic analysis service to find symbol at position
            var symbolAnalysis = await _semanticAnalysis.AnalyzeSymbolAtPositionAsync(document, position, cancellationToken);
            if (symbolAnalysis == null)
            {
                return new SymbolNavigationResult
                {
                    Success = false,
                    Error = "No symbol found at the specified position"
                };
            }

            var symbol = symbolAnalysis.Symbol;
            var locations = new List<SymbolLocation>();

            // Find all references using Roslyn's FindSymbols API
            var references = await SymbolFinder.FindReferencesAsync(symbol, currentSolution, cancellationToken);

            foreach (var reference in references.Take(options.MaxResults))
            {
                // Add definition locations if requested
                if (options.IncludeDefinitions || options.IncludeDeclaration)
                {
                    foreach (var location in reference.Definition.Locations)
                    {
                        if (location.IsInSource)
                        {
                            var symbolLocation = await CreateSymbolLocationAsync(location, "Definition", cancellationToken);
                            if (symbolLocation != null)
                            {
                                locations.Add(symbolLocation);
                            }
                        }
                    }
                }

                // Add reference locations if requested
                if (options.IncludeReferences)
                {
                    foreach (var referenceLocation in reference.Locations)
                    {
                        if (referenceLocation.Location.IsInSource)
                        {
                            var symbolLocation = await CreateSymbolLocationAsync(
                                referenceLocation.Location,
                                "Reference",
                                cancellationToken);

                            if (symbolLocation != null)
                            {
                                symbolLocation.Context = referenceLocation.CandidateReason.ToString();
                                locations.Add(symbolLocation);
                            }
                        }
                    }
                }
            }

            // Find implementations if requested (for interfaces and virtual members)
            if (options.IncludeImplementations && symbol is INamedTypeSymbol namedTypeSymbol)
            {
                var implementations = await SymbolFinder.FindImplementationsAsync(namedTypeSymbol, currentSolution, false, null, cancellationToken);
                foreach (var impl in implementations.Take(50)) // Limit implementations
                {
                    foreach (var location in impl.Locations)
                    {
                        if (location.IsInSource)
                        {
                            var symbolLocation = await CreateSymbolLocationAsync(location, "Implementation", cancellationToken);
                            if (symbolLocation != null)
                            {
                                locations.Add(symbolLocation);
                            }
                        }
                    }
                }
            }

            // Remove duplicates and sort
            locations = locations
                .GroupBy(l => $"{l.FilePath}:{l.LineNumber}:{l.Column}")
                .Select(g => g.First())
                .OrderBy(l => l.FilePath)
                .ThenBy(l => l.LineNumber)
                .ThenBy(l => l.Column)
                .ToList();

            var result = new SymbolNavigationResult
            {
                Success = true,
                Message = $"Found {locations.Count} reference(s) to '{symbol.Name}'",
                Locations = locations,
                Symbol = symbolAnalysis.NavigationInfo,
                Metadata =
                {
                    ["searchedPosition"] = $"{filePath}:{lineNumber}:{column}",
                    ["symbolKind"] = symbol.Kind.ToString(),
                    ["includeDefinitions"] = options.IncludeDefinitions,
                    ["includeReferences"] = options.IncludeReferences,
                    ["includeImplementations"] = options.IncludeImplementations,
                    ["workspaceMode"] = _workspaceManagement.IsUsingFallbackWorkspace ? "Fallback" : "MSBuild",
                    ["environmentInitialized"] = _workspaceManagement.IsEnvironmentInitialized
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            return new SymbolNavigationResult
            {
                Success = false,
                Error = $"Find references failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Find symbols by name across the entire workspace
    /// </summary>
    public async Task<SymbolNavigationResult> FindSymbolsByNameAsync(
        string symbolName,
        bool exactMatch = false,
        SymbolKind? symbolKind = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentSolution = _workspaceManagement.CurrentSolution;
            if (currentSolution == null)
            {
                var refreshed = await _workspaceManagement.RefreshWorkspaceAsync(cancellationToken);
                currentSolution = _workspaceManagement.CurrentSolution;
                if (!refreshed || currentSolution == null)
                {
                    return new SymbolNavigationResult
                    {
                        Success = false,
                        Error = "Unable to load workspace for symbol search. The developer environment could not be initialized properly."
                    };
                }
            }

            var locations = new List<SymbolLocation>();

            // Use semantic analysis service to find symbols by name
            var foundSymbols = await _semanticAnalysis.FindSymbolsByNameAsync(
                currentSolution, symbolName, exactMatch, symbolKind, maxResults, cancellationToken);

            // Convert symbols to locations
            foreach (var symbol in foundSymbols.Take(maxResults))
            {
                foreach (var location in symbol.Locations)
                {
                    if (location.IsInSource)
                    {
                        var symbolLocation = await CreateSymbolLocationAsync(location, "Declaration", cancellationToken);
                        if (symbolLocation != null)
                        {
                            symbolLocation.Context = $"{symbol.Kind} in {symbol.ContainingType?.Name ?? symbol.ContainingNamespace?.Name ?? "Global"}";
                            locations.Add(symbolLocation);
                        }
                    }
                }
            }

            var result = new SymbolNavigationResult
            {
                Success = true,
                Message = $"Found {locations.Count} symbol(s) matching '{symbolName}'",
                Locations = locations,
                Metadata =
                {
                    ["searchQuery"] = symbolName,
                    ["exactMatch"] = exactMatch,
                    ["symbolKind"] = symbolKind?.ToString() ?? "Any",
                    ["workspaceMode"] = _workspaceManagement.IsUsingFallbackWorkspace ? "Fallback" : "MSBuild",
                    ["environmentInitialized"] = _workspaceManagement.IsEnvironmentInitialized
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            return new SymbolNavigationResult
            {
                Success = false,
                Error = $"Symbol search failed: {ex.Message}"
            };
        }
    }

    private static int GetPosition(SourceText sourceText, int lineNumber, int column)
    {
        try
        {
            // Convert 1-based line/column to 0-based
            var line = Math.Max(0, lineNumber - 1);
            var col = Math.Max(0, column - 1);

            if (line >= sourceText.Lines.Count)
                return -1;

            var textLine = sourceText.Lines[line];
            var position = textLine.Start + Math.Min(col, textLine.Span.Length);

            return position;
        }
        catch
        {
            return -1;
        }
    }

    private static async Task<SymbolLocation?> CreateSymbolLocationAsync(
        Microsoft.CodeAnalysis.Location location,
        string locationType,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!location.IsInSource || location.SourceTree == null)
                return null;

            var sourceText = await location.SourceTree.GetTextAsync(cancellationToken);
            var linePosition = location.GetLineSpan().StartLinePosition;

            // Get preview text (the line containing the symbol)
            var preview = linePosition.Line < sourceText.Lines.Count
                ? sourceText.Lines[linePosition.Line].ToString().Trim()
                : "";

            return new SymbolLocation
            {
                FilePath = location.SourceTree.FilePath,
                LineNumber = linePosition.Line + 1,
                Column = linePosition.Character + 1,
                Preview = preview,
                LocationType = locationType
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get environment initialization status - delegates to workspace management service
    /// </summary>
    public Dictionary<string, string> GetEnvironmentStatus()
    {
        return _workspaceManagement.GetEnvironmentStatus();
    }

    public void Dispose()
    {
        _workspaceManagement?.Dispose();
    }
}
