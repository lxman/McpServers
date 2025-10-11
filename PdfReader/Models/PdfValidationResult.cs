namespace PdfMcp.Models;

public class PdfValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string PdfVersion { get; set; } = string.Empty;
    public bool IsPasswordProtected { get; set; }
    public bool IsCorrupted { get; set; }
}