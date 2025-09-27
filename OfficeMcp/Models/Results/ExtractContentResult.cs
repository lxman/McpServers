namespace OfficeMcp.Models.Results;

public class ExtractContentResult
{
    public string FilePath { get; set; } = "";
    public DocumentType DocumentType { get; set; }
    public string PlainText { get; set; } = "";
    public int WordCount { get; set; }
    public List<ExtractedTable> Tables { get; set; } = [];
    public List<ExtractedImage> Images { get; set; } = [];
}