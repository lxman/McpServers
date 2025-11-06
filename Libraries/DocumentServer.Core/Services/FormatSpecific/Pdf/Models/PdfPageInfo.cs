namespace DocumentServer.Core.Services.FormatSpecific.Pdf.Models;

public class PdfPageInfo
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public int ImageCount { get; set; }
    public int CharacterCount { get; set; }
    public int WordCount { get; set; }
}
