namespace PdfMcp.Models;

public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string MatchedText { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public double RelevanceScore { get; set; }
}