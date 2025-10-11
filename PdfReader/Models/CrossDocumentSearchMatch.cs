namespace PdfMcp.Models;

public class CrossDocumentSearchMatch
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int PageNumber { get; set; }
    public string MatchedText { get; set; } = "";
    public string Context { get; set; } = "";
    public double RelevanceScore { get; set; }
}