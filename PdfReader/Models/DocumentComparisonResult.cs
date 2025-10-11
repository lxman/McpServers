namespace PdfMcp.Models;

public class DocumentComparisonResult
{
    public DocumentComparisonInfo Document1 { get; set; } = new();
    public DocumentComparisonInfo Document2 { get; set; } = new();
    public ComparisonMetrics Comparison { get; set; } = new();
}