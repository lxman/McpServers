namespace PdfMcp.Models;

public class TextExtractionResult
{
    public string FilePath { get; set; } = "";
    public int PageCount { get; set; }
    public string FullText { get; set; } = "";
    public int WordCount { get; set; }
}