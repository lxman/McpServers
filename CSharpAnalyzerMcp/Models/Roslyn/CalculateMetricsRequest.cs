namespace CSharpAnalyzerMcp.Models.Roslyn;

public class CalculateMetricsRequest
{
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}
