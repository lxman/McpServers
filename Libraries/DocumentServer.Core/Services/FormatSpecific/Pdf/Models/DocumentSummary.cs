namespace DocumentServer.Core.Services.FormatSpecific.Pdf.Models;

public class DocumentSummary
{
    public int WordCount { get; set; }
    public List<string> KeyPoints { get; set; } = [];
    public Dictionary<string, int> KeywordFrequency { get; set; } = new();
    public string MainContent { get; set; } = string.Empty;
}
