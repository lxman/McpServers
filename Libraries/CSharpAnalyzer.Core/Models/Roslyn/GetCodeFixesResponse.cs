namespace CSharpAnalyzer.Core.Models.Roslyn;

public class GetCodeFixesResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<CodeFixInfo> CodeFixes { get; set; } = new();
    public int TotalCount { get; set; }
}

public class CodeFixInfo
{
    public string? DiagnosticId { get; set; }
    public string? DiagnosticMessage { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string? FixDescription { get; set; }
    public string? FixedCode { get; set; }
}
