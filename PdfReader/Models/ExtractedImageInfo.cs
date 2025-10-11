namespace PdfMcp.Models;

public class ExtractedImageInfo
{
    public int PageNumber { get; set; }
    public int ImageIndex { get; set; }
    public string FileName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}