namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to extract text from a scanned PDF using OCR
/// </summary>
public class OcrPdfRequest
{
    /// <summary>
    /// Path to the PDF file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional password for encrypted PDFs
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Language code for OCR (e.g., "eng", "spa", default: "eng")
    /// </summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    /// Whether to preprocess images for better OCR results (default: true)
    /// </summary>
    public bool PreprocessImages { get; set; } = true;
}
