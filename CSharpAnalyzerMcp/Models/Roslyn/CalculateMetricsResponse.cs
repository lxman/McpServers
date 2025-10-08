namespace CSharpAnalyzerMcp.Models.Roslyn;

public class CalculateMetricsResponse
{
    public bool Success { get; set; }
    public int TotalLines { get; set; }
    public int TotalClasses { get; set; }
    public int TotalMethods { get; set; }
    public int TotalProperties { get; set; }
    public double AverageCyclomaticComplexity { get; set; }
    public int MaxCyclomaticComplexity { get; set; }
    public string? Error { get; set; }
}
