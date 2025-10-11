namespace PdfMcp.Models;

public class CrossDocumentSearchResult
{
    public string SearchTerm { get; set; } = "";
    public int TotalDocuments { get; set; }
    public int TotalResults { get; set; }
    public List<CrossDocumentSearchMatch> Results { get; set; } = [];
}