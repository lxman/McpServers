namespace PdfMcp.Models;

public class PageContentResult
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = "";
    public PageDimensions Dimensions { get; set; } = new();
    public int Rotation { get; set; }
    public List<PdfImage> Images { get; set; } = [];
    public List<PdfAnnotation> Annotations { get; set; } = [];
    public List<PdfLink> Links { get; set; } = [];
}