namespace PdfMcp.Models;

public class ComparisonMetrics
{
    public double TextSimilarity { get; set; }
    public int CommonWords { get; set; }
    public int UniqueToDoc1 { get; set; }
    public int UniqueToDoc2 { get; set; }
    public int PageDifference { get; set; }
    public long SizeDifference { get; set; }
}