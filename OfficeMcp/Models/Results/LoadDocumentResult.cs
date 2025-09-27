namespace OfficeMcp.Models.Results;

public class LoadDocumentResult
{
    public string Message { get; set; } = "";
    public OfficeMetadata Metadata { get; set; } = new();
    public DocumentType DocumentType { get; set; }
}