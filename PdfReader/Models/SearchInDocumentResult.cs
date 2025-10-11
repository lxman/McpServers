namespace PdfMcp.Models;

public class SearchInDocumentResult
{
    public string SearchTerm { get; set; } = "";
    public int TotalResults { get; set; }
    public List<SearchResult> Results { get; set; } = [];
}