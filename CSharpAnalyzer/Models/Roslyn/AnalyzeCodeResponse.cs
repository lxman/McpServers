namespace CSharpAnalyzer.Models.Roslyn;

public class AnalyzeCodeResponse
{
    public List<DiagnosticInfo> Diagnostics { get; set; } = [];
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public bool Success { get; set; }
}
