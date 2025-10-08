namespace CSharpAnalyzerMcp.Models.Roslyn;

public class FormatCodeResponse
{
    public bool Success { get; set; }
    public string FormattedCode { get; set; } = string.Empty;
    public string OriginalCode { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool WasChanged { get; set; }
    public string? ErrorMessage { get; set; }
}