namespace CSharpAnalyzerMcp.Models.Roslyn;

public class AnalyzeCodeRequest
{
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}
