namespace OfficeMcp.Models.Results;

public class SearchDocumentResult
{
    public string FilePath { get; set; } = "";
    public string SearchTerm { get; set; } = "";
    public DocumentType DocumentType { get; set; }
    public int TotalResults { get; set; }
    public List<OfficeSearchResult> Results { get; set; } = [];
}