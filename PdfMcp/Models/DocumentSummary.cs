namespace PdfMcp.Models;

public class DocumentSummary
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string MainContent { get; set; } = string.Empty;
    public List<string> KeyPoints { get; set; } = [];
    public List<string> Topics { get; set; } = [];
    public int WordCount { get; set; }
    public int PageCount { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public Dictionary<string, int> KeywordFrequency { get; set; } = new();
}