using Microsoft.CodeAnalysis;
using McpCodeEditor.Models;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for semantic analysis operations including symbol information extraction,
/// semantic model operations, and symbol discovery
/// </summary>
public interface ISemanticAnalysisService
{
    /// <summary>
    /// Get semantic model for a document with error handling
    /// </summary>
    Task<SemanticModel?> GetSemanticModelAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find symbol at the specified position in a document
    /// </summary>
    Task<ISymbol?> FindSymbolAtPositionAsync(
        Document document, 
        int position, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create NavigationSymbolInfo from ISymbol with comprehensive analysis
    /// </summary>
    NavigationSymbolInfo CreateNavigationSymbolInfo(ISymbol symbol);

    /// <summary>
    /// Get symbol filter for Roslyn APIs based on SymbolKind
    /// </summary>
    SymbolFilter GetSymbolFilter(SymbolKind symbolKind);

    /// <summary>
    /// Find symbols by name in the solution with filtering and search options
    /// </summary>
    Task<IEnumerable<ISymbol>> FindSymbolsByNameAsync(
        Solution solution,
        string symbolName,
        bool exactMatch = false,
        SymbolKind? symbolKind = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze symbol at position and return detailed symbol information
    /// Returns null if no symbol found at position
    /// </summary>
    Task<SymbolAnalysisResult?> AnalyzeSymbolAtPositionAsync(
        Document document,
        int position,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of symbol analysis at a specific position
/// </summary>
public class SymbolAnalysisResult
{
    public ISymbol Symbol { get; set; } = null!;
    public SyntaxToken Token { get; set; }
    public SymbolInfo SymbolInfo { get; set; }
    public SemanticModel SemanticModel { get; set; } = null!;
    public NavigationSymbolInfo NavigationInfo { get; set; } = null!;
}
