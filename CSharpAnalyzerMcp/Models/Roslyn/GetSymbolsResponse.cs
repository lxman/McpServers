namespace CSharpAnalyzerMcp.Models.Roslyn;

public class GetSymbolsResponse
{
    public List<SymbolInfo> Symbols { get; set; } = [];
    public int TotalCount { get; set; }
}
