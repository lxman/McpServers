using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for semantic analysis operations including symbol information extraction,
/// semantic model operations, and symbol discovery
/// </summary>
public class SemanticAnalysisService : ISemanticAnalysisService
{
    private readonly ILogger<SemanticAnalysisService>? _logger;

    public SemanticAnalysisService(ILogger<SemanticAnalysisService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get semantic model for a document with error handling
    /// </summary>
    public async Task<SemanticModel?> GetSemanticModelAsync(Document document, CancellationToken cancellationToken = default)
    {
        try
        {
            return await document.GetSemanticModelAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get semantic model for document: {DocumentPath}", document.FilePath);
            return null;
        }
    }

    /// <summary>
    /// Find symbol at the specified position in a document
    /// </summary>
    public async Task<ISymbol?> FindSymbolAtPositionAsync(
        Document document, 
        int position, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var semanticModel = await GetSemanticModelAsync(document, cancellationToken);
            if (semanticModel == null)
                return null;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var token = root?.FindToken(position);

            if (token == null || token.Value.IsKind(SyntaxKind.None))
                return null;

            var symbolInfo = semanticModel.GetSymbolInfo(token.Value.Parent!, cancellationToken);
            return symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(token.Value.Parent!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to find symbol at position {Position} in document: {DocumentPath}", position, document.FilePath);
            return null;
        }
    }

    /// <summary>
    /// Create NavigationSymbolInfo from ISymbol with comprehensive analysis
    /// </summary>
    public NavigationSymbolInfo CreateNavigationSymbolInfo(ISymbol symbol)
    {
        return new NavigationSymbolInfo
        {
            Name = symbol.Name,
            FullName = symbol.ToDisplayString(),
            Kind = symbol.Kind.ToString(),
            ContainingType = symbol.ContainingType?.Name ?? "",
            Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? "",
            AssemblyName = symbol.ContainingAssembly?.Name ?? "",
            IsFromSource = symbol.Locations.Any(l => l.IsInSource),
            Documentation = symbol.GetDocumentationCommentXml(),
            Properties = new Dictionary<string, object>
            {
                ["accessibility"] = symbol.DeclaredAccessibility.ToString(),
                ["isStatic"] = symbol.IsStatic,
                ["isVirtual"] = symbol.IsVirtual,
                ["isAbstract"] = symbol.IsAbstract,
                ["isSealed"] = symbol.IsSealed
            }
        };
    }

    /// <summary>
    /// Get symbol filter for Roslyn APIs based on SymbolKind
    /// </summary>
    public SymbolFilter GetSymbolFilter(SymbolKind symbolKind)
    {
        return symbolKind switch
        {
            SymbolKind.NamedType => SymbolFilter.Type,
            SymbolKind.Method => SymbolFilter.Member,
            SymbolKind.Property => SymbolFilter.Member,
            SymbolKind.Field => SymbolFilter.Member,
            SymbolKind.Event => SymbolFilter.Member,
            SymbolKind.Namespace => SymbolFilter.Namespace,
            _ => SymbolFilter.All
        };
    }

    /// <summary>
    /// Find symbols by name in the solution with filtering and search options
    /// </summary>
    public async Task<IEnumerable<ISymbol>> FindSymbolsByNameAsync(
        Solution solution,
        string symbolName,
        bool exactMatch = false,
        SymbolKind? symbolKind = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Search for symbols by name - Using simpler overload without filter
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
                solution,
                symbolName,
                !exactMatch,
                cancellationToken);

            // Filter by symbol kind if specified
            if (symbolKind.HasValue)
            {
                symbols = symbols.Where(s => s.Kind == symbolKind.Value);
            }

            return symbols.Take(maxResults);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to find symbols by name: {SymbolName}", symbolName);
            return [];
        }
    }

    /// <summary>
    /// Analyze symbol at position and return detailed symbol information
    /// Returns null if no symbol found at position
    /// </summary>
    public async Task<SymbolAnalysisResult?> AnalyzeSymbolAtPositionAsync(
        Document document,
        int position,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var semanticModel = await GetSemanticModelAsync(document, cancellationToken);
            if (semanticModel == null)
                return null;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var token = root?.FindToken(position);

            if (token == null || token.Value.IsKind(SyntaxKind.None))
                return null;

            var symbolInfo = semanticModel.GetSymbolInfo(token.Value.Parent!, cancellationToken);
            var symbol = symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(token.Value.Parent!, cancellationToken);

            if (symbol == null)
                return null;

            return new SymbolAnalysisResult
            {
                Symbol = symbol,
                Token = token.Value,
                SymbolInfo = symbolInfo,
                SemanticModel = semanticModel,
                NavigationInfo = CreateNavigationSymbolInfo(symbol)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to analyze symbol at position {Position} in document: {DocumentPath}", position, document.FilePath);
            return null;
        }
    }
}
