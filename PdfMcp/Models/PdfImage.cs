namespace PdfMcp.Models;

public class PdfImage
{
    public int ImageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string ImageType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string ColorSpace { get; set; } = string.Empty;
}