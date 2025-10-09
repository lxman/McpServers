namespace CSharpAnalyzerMcp.Models.Roslyn;

public class CheckDocumentationCoverageResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<UndocumentedSymbol> UndocumentedSymbols { get; set; } = new();
    public int TotalPublicSymbols { get; set; }
    public int DocumentedSymbols { get; set; }
    public int UndocumentedSymbolsCount { get; set; }
    public double CoveragePercentage { get; set; }
}

public class UndocumentedSymbol
{
    public string SymbolName { get; set; } = string.Empty;
    public string SymbolKind { get; set; } = string.Empty;
    public string FullyQualifiedName { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}
