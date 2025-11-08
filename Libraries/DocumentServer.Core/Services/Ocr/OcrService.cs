using System.Text;
using DocumentServer.Core.Services.Ocr.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using PdfDocument = UglyToad.PdfPig.PdfDocument;
using Page = UglyToad.PdfPig.Content.Page;

namespace DocumentServer.Core.Services.Ocr;

/// <summary>
/// Provides OCR (Optical Character Recognition) services for documents and images
/// </summary>
public class OcrService : IDisposable
{
    private readonly ILogger<OcrService> _logger;
    private readonly TesseractEngine _tesseractEngine;
    private readonly ImagePreprocessor _imagePreprocessor;
    private bool _disposed;

    /// <summary>
    /// Indicates whether OCR services are available
    /// </summary>
    public bool IsAvailable => _tesseractEngine.IsAvailable;

    public OcrService(
        ILogger<OcrService> logger,
        TesseractEngine tesseractEngine,
        ImagePreprocessor imagePreprocessor)
    {
        _logger = logger;
        _tesseractEngine = tesseractEngine;
        _imagePreprocessor = imagePreprocessor;

        if (IsAvailable)
        {
            _logger.LogInformation("OCR service initialized successfully");
        }
        else
        {
            _logger.LogWarning("OCR service initialized but Tesseract is not available");
        }
    }

    /// <summary>
    /// Extract text from password-protected scanned PDF using OCR
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file</param>
    /// <param name="password">Optional password for encrypted PDFs</param>
    /// <returns>OCR result containing extracted text and metadata</returns>
    public async Task<OcrResult> ExtractTextFromScannedPdf(string pdfPath, string? password = null)
    {
        if (!IsAvailable)
        {
            return new OcrResult
            {
                Success = false,
                ErrorMessage = "OCR service is not available"
            };
        }

        try
        {
            _logger.LogInformation("Starting OCR extraction from PDF: {PdfPath}", pdfPath);

            // Set up PdfPig parsing options with password support
            var parsingOptions = new ParsingOptions();
            if (!string.IsNullOrEmpty(password))
            {
                parsingOptions.Passwords = [password];
                _logger.LogDebug("Using password for PDF decryption");
            }

            var result = new OcrResult
            {
                Success = true,
                Metadata = new Dictionary<string, string>
                {
                    ["FilePath"] = pdfPath,
                    ["ProcessedAt"] = DateTime.UtcNow.ToString("O")
                }
            };

            var allText = new StringBuilder();

            using PdfDocument pdfDocument = PdfDocument.Open(pdfPath, parsingOptions);
            
            int totalPages = pdfDocument.NumberOfPages;
            result.Metadata["TotalPages"] = totalPages.ToString();
            _logger.LogInformation("Processing {PageCount} pages from PDF", totalPages);

            foreach (Page page in pdfDocument.GetPages())
            {
                try
                {
                    // First, check if there's already extractable text
                    string existingText = page.Text;
                    
                    if (IsTextMeaningful(existingText))
                    {
                        allText.AppendLine(existingText);
                        _logger.LogDebug("Page {PageNumber} has extractable text, skipping OCR", page.Number);
                    }
                    else
                    {
                        _logger.LogDebug("Page {PageNumber} appears to be scanned, using OCR", page.Number);
                        
                        // Extract images from the page and perform OCR
                        string ocrText = await ExtractTextFromPdfPage(page);
                        allText.AppendLine(ocrText);
                        result.PagesProcessed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process page {PageNumber} of PDF {PdfPath}", 
                        page.Number, pdfPath);
                    allText.AppendLine($"[OCR Error on page {page.Number}: {ex.Message}]");
                    result.PagesWithErrors++;
                    result.Warnings.Add($"Page {page.Number}: {ex.Message}");
                }
            }

            result.ExtractedText = allText.ToString();
            result.Metadata["ExtractedLength"] = result.ExtractedText.Length.ToString();

            _logger.LogInformation("Completed OCR extraction from PDF. Processed: {Processed}, Errors: {Errors}", 
                result.PagesProcessed, result.PagesWithErrors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from scanned PDF: {PdfPath}", pdfPath);
            return new OcrResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Extract text from a single PDF page using OCR
    /// </summary>
    private async Task<string> ExtractTextFromPdfPage(Page page)
    {
        try
        {
            return await Task.Run(() =>
            {
                var allOcrText = new StringBuilder();
                
                // Get images from the page
                IEnumerable<IPdfImage> images = page.GetImages().ToList();
                
                if (!images.Any())
                {
                    _logger.LogDebug("No images found on page {PageNumber}, page might be text-based or empty", 
                        page.Number);
                    return string.Empty;
                }

                _logger.LogDebug("Found {ImageCount} images on page {PageNumber}", 
                    images.Count(), page.Number);

                foreach (IPdfImage pdfImage in images)
                {
                    try
                    {
                        // Convert PdfPig image to bytes
                        byte[] imageBytes = pdfImage.RawBytes.ToArray();
                        
                        // Enhance the image using ImagePreprocessor
                        byte[] enhancedImageBytes = _imagePreprocessor.EnhanceImageForOcr(imageBytes);
                        
                        // Perform OCR on the enhanced image
                        string ocrText = _tesseractEngine.ExtractText(enhancedImageBytes);
                        
                        if (!string.IsNullOrWhiteSpace(ocrText))
                        {
                            allOcrText.AppendLine(ocrText);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process image on page {PageNumber}", page.Number);
                    }
                }
                
                return allOcrText.ToString();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform OCR on PDF page {PageNumber}", page.Number);
            return $"[OCR failed on page {page.Number}: {ex.Message}]";
        }
    }

    /// <summary>
    /// Extracts text from an image file using OCR
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="enhanceImage">Whether to apply image enhancement before OCR (default: true)</param>
    /// <returns>OCR result containing extracted text and metadata</returns>
    public async Task<OcrResult> ExtractTextFromImage(string imagePath, bool enhanceImage = true)
    {
        if (!IsAvailable)
        {
            return new OcrResult
            {
                Success = false,
                ErrorMessage = "OCR service is not available"
            };
        }

        try
        {
            _logger.LogInformation("Starting OCR extraction from image: {ImagePath}", imagePath);

            OcrResult result = await Task.Run(() =>
            {
                // Load image
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                
                // Optionally enhance the image
                if (enhanceImage)
                {
                    imageBytes = _imagePreprocessor.EnhanceImageForOcr(imageBytes);
                }
                
                // Perform OCR with confidence
                (string text, float confidence) = _tesseractEngine.ExtractTextWithConfidence(imageBytes);
                
                return new OcrResult
                {
                    Success = true,
                    ExtractedText = text,
                    PagesProcessed = 1,
                    Confidence = confidence,
                    Metadata = new Dictionary<string, string>
                    {
                        ["FilePath"] = imagePath,
                        ["ProcessedAt"] = DateTime.UtcNow.ToString("O"),
                        ["ImageEnhanced"] = enhanceImage.ToString(),
                        ["ExtractedLength"] = text.Length.ToString()
                    }
                };
            });

            _logger.LogInformation("Completed OCR extraction from image. Confidence: {Confidence:P1}", 
                result.Confidence ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from image: {ImagePath}", imagePath);
            return new OcrResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Determines if a PDF is likely scanned by checking text content
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file</param>
    /// <param name="password">Optional password for encrypted PDFs</param>
    /// <returns>True if the PDF appears to be scanned (contains primarily images rather than text)</returns>
    public bool IsPdfScanned(string pdfPath, string? password = null)
    {
        try
        {
            _logger.LogDebug("Analyzing PDF for scanned content: {PdfPath}", pdfPath);

            var parsingOptions = new ParsingOptions();
            if (!string.IsNullOrEmpty(password))
            {
                parsingOptions.Passwords = [password];
            }

            using PdfDocument pdfDocument = PdfDocument.Open(pdfPath, parsingOptions);
            
            int pagesToCheck = Math.Min(pdfDocument.NumberOfPages, 3); // Check the first 3 pages
            int textlessPages = pdfDocument.GetPages()
                .Take(pagesToCheck)
                .Count(page => !IsTextMeaningful(page.Text));

            // If more than half the checked pages have no meaningful text, likely scanned
            bool isScanned = textlessPages > pagesToCheck / 2;

            _logger.LogInformation("PDF scan analysis: {ScannedPages}/{TotalChecked} pages without text. Scanned: {IsScanned}",
                textlessPages, pagesToCheck, isScanned);

            return isScanned;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze PDF for scanned content: {PdfPath}", pdfPath);
            return false;
        }
    }

    /// <summary>
    /// Checks if extracted text is meaningful (not just whitespace or garbage)
    /// </summary>
    private static bool IsTextMeaningful(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string cleanText = text.Trim();
        if (cleanText.Length < 10)
            return false;

        // Check if it contains mostly readable characters
        int readableChars = cleanText.Count(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || c == ' ');
        double readableRatio = (double)readableChars / cleanText.Length;

        return readableRatio > 0.7; // At least 70% readable characters
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _tesseractEngine?.Dispose();
        _disposed = true;
        _logger.LogDebug("OCR service disposed");
    }
}
