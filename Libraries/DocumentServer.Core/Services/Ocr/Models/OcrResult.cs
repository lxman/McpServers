namespace DocumentServer.Core.Services.Ocr.Models;

/// <summary>
/// Represents the result of an OCR operation
/// </summary>
public class OcrResult
{
    /// <summary>
    /// Whether the OCR operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The extracted text from the OCR operation
    /// </summary>
    public string ExtractedText { get; set; } = string.Empty;

    /// <summary>
    /// Number of pages or images processed
    /// </summary>
    public int PagesProcessed { get; set; }

    /// <summary>
    /// Number of pages or images that had errors
    /// </summary>
    public int PagesWithErrors { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) if available
    /// </summary>
    public float? Confidence { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of warnings encountered during processing
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Metadata about the OCR operation
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
