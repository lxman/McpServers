namespace CSharpAnalyzerMcp.Models.Roslyn;

public class FormatCodeRequest
{
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}
