namespace CSharpAnalyzerMcp.Models.Roslyn;

public class CheckNamingConventionsResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<NamingViolation> Violations { get; set; } = new();
    public int TotalViolations { get; set; }
}

public class NamingViolation
{
    public string SymbolName { get; set; } = string.Empty;
    public string SymbolKind { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string ExpectedPattern { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}
