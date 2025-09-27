namespace OfficeMcp.Models.Results;

public class CrossDocumentSearchResult
{
    public string SearchTerm { get; set; } = "";
    public int TotalDocuments { get; set; }
    public int TotalResults { get; set; }
    public List<OfficeSearchResult> Results { get; set; } = [];
}