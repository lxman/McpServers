namespace CSharpAnalyzer.Core.Models.Roslyn;

public class GetTypeInfoResponse
{
    public bool Success { get; set; }
    public string? TypeName { get; set; }
    public string? SymbolName { get; set; }
    public string? ContainingType { get; set; }
    public string? Error { get; set; }
    public string? FullTypeName { get; set; }
    public string? SymbolKind { get; set; }
    public string? Documentation { get; set; }
    public List<string> Interfaces { get; set; } = [];
    public string? BaseType { get; set; }
    public bool IsNullable { get; set; }
    public bool IsValueType { get; set; }
    public bool IsReferenceType { get; set; }
    public string? ErrorMessage { get; set; }
}