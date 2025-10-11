namespace PdfMcp.Models;

public class PageAnalysis
{
    public int PageNumber { get; set; }
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public int ImageCount { get; set; }
    public int AnnotationCount { get; set; }
    public int LinkCount { get; set; }
    public PageDimensions Dimensions { get; set; } = new();
    public int Rotation { get; set; }
    public bool HasContent { get; set; }
}