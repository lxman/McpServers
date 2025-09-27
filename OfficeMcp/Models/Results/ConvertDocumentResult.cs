namespace OfficeMcp.Models.Results;

public class ConvertDocumentResult
{
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public DocumentType SourceType { get; set; }
    public DocumentType TargetType { get; set; }
    public bool Success { get; set; }
    public long OutputFileSize { get; set; }
}