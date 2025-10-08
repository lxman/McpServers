namespace CSharpAnalyzerMcp.Models.Roslyn;

public class SymbolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ContainingType { get; set; } = string.Empty;
    public string Accessibility { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsVirtual { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}