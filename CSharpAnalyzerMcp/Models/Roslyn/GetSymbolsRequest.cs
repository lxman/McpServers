namespace CSharpAnalyzerMcp.Models.Roslyn;

public class GetSymbolsRequest
{
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? Filter { get; set; } // e.g., "class", "method", "property"
}
