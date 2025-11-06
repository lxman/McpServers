namespace DocumentServer.Core.Services.FormatSpecific.Pdf.Models;

public class PdfImageInfo
{
    public int ImageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public int SizeInBytes { get; set; }
    public byte[]? ImageData { get; set; }
}
