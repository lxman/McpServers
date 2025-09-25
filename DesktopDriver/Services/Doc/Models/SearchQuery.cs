namespace DesktopDriver.Services.Doc.Models;

public class SearchQuery
{
    public string Query { get; set; } = string.Empty;
    public List<string> FileTypes { get; set; } = [];
    public List<string> Directories { get; set; } = [];
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public double MinRelevance { get; set; } = 0.0;
    public int MaxResults { get; set; } = 50;
    public bool IncludeSnippets { get; set; } = true;
    public bool GroupByDirectory { get; set; } = false;
    public string SortBy { get; set; } = "relevance"; // relevance, date, title, path
    public bool SortDescending { get; set; } = true;
}