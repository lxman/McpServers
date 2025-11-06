namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to extract text from an image file using OCR
/// </summary>
public class OcrImageRequest
{
    /// <summary>
    /// Path to the image file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Language code for OCR (e.g., "eng", "spa", default: "eng")
    /// </summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    /// Whether to preprocess image for better OCR results (default: true)
    /// </summary>
    public bool PreprocessImage { get; set; } = true;
}
