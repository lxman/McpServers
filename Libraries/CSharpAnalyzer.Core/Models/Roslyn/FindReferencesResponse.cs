namespace CSharpAnalyzer.Core.Models.Roslyn;

public class FindReferencesResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ReferenceInfo> References { get; set; } = new();
    public int TotalCount { get; set; }
    public string? SymbolName { get; set; }
    public string? SymbolKind { get; set; }
}

public class ReferenceInfo
{
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string? FilePath { get; set; }
    public string? Context { get; set; } // Surrounding code context
}
