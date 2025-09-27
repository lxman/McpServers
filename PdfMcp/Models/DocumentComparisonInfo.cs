namespace PdfMcp.Models;

public class DocumentComparisonInfo
{
    public string FileName { get; set; } = "";
    public int PageCount { get; set; }
    public int WordCount { get; set; }
    public long FileSize { get; set; }
}