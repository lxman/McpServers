namespace OfficeReader.Models.Results;

public class OfficeSearchResult
{
    public string FilePath { get; set; } = "";
    public DocumentType DocumentType { get; set; }
    public string Location { get; set; } = "";
    public string MatchedText { get; set; } = "";
    public string Context { get; set; } = "";
    public double RelevanceScore { get; set; }
    
    // Location-specific details
    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public string? WorksheetName { get; set; }
    public string? CellAddress { get; set; }
    public string? SectionTitle { get; set; }
}