using System.Text;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using TesseractOCR;
using TesseractOCR.Enums;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using TesseractImage = TesseractOCR.Pix.Image;
using PdfDocument = UglyToad.PdfPig.PdfDocument;
using Page = UglyToad.PdfPig.Content.Page;

namespace DesktopDriver.Services.DocumentSearching;

public class OcrService : IDisposable
{
    public bool IsAvailable { get; }
    
    private readonly ILogger<OcrService> _logger;
    private readonly Engine? _tesseractEngine;

    public OcrService(ILogger<OcrService> logger)
    {
        _logger = logger;
        
        try
        {
            string tessdataPath = GetTessdataPath();
            
            if (Directory.Exists(tessdataPath))
            {
                _tesseractEngine = new Engine(tessdataPath, Language.English, EngineMode.Default);
                IsAvailable = true;
                _logger.LogInformation("OCR service initialized successfully with Tesseract");
            }
            else
            {
                _logger.LogWarning("Tessdata directory not found at: {TessdataPath}. OCR features will be disabled.", tessdataPath);
                IsAvailable = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Tesseract OCR engine. OCR features will be disabled.");
            IsAvailable = false;
        }
    }

    /// <summary>
    /// Extract text from password-protected scanned PDF using PdfPig + SixLabors + Tesseract
    /// This approach handles passwords natively without temporary files!
    /// </summary>
    public async Task<string> ExtractTextFromScannedPdf(string pdfPath, string? password = null)
    {
        if (!IsAvailable || _tesseractEngine == null)
        {
            throw new InvalidOperationException("OCR service is not available");
        }

        try
        {
            // Set up PdfPig parsing options with password support
            var parsingOptions = new ParsingOptions();
            if (!string.IsNullOrEmpty(password))
            {
                parsingOptions.Passwords = [password];
            }

            var allText = new StringBuilder();

            using PdfDocument pdfDocument = PdfDocument.Open(pdfPath, parsingOptions);
            
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
                        string ocrText = await ExtractTextFromPdfPageWithPdfPig(page);
                        allText.AppendLine(ocrText);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process page {PageNumber} of PDF {PdfPath}", page.Number, pdfPath);
                    allText.AppendLine($"[OCR Error on page {page.Number}: {ex.Message}]");
                }
            }

            return allText.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from scanned PDF: {PdfPath}", pdfPath);
            throw;
        }
    }

    /// <summary>
    /// Extract text from PDF page using PdfPig + SixLabors + Tesseract workflow
    /// </summary>
    private async Task<string> ExtractTextFromPdfPageWithPdfPig(Page page)
    {
        if (_tesseractEngine == null)
            throw new InvalidOperationException("Tesseract engine not initialized");

        try
        {
            return await Task.Run(() =>
            {
                var allOcrText = new StringBuilder();
                
                // Get images from the page
                IEnumerable<IPdfImage> images = page.GetImages().ToList();
                
                if (!images.Any())
                {
                    _logger.LogDebug("No images found on page {PageNumber}, page might be text-based or empty", page.Number);
                    return string.Empty;
                }

                foreach (IPdfImage pdfImage in images)
                {
                    try
                    {
                        // Convert PdfPig image to bytes
                        byte[] imageBytes = pdfImage.RawBytes.ToArray();
                        
                        // Enhance an image using SixLabors ImageSharp before OCR
                        byte[] enhancedImageBytes = EnhanceImageForOcr(imageBytes);
                        
                        // Perform OCR on the enhanced image
                        using TesseractImage? img = TesseractImage.LoadFromMemory(enhancedImageBytes);
                        using TesseractOCR.Page? ocrPage = _tesseractEngine.Process(img);
                        
                        string ocrText = ocrPage.Text;
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
    /// Enhance image using SixLabors ImageSharp for better OCR accuracy
    /// </summary>
    private byte[] EnhanceImageForOcr(byte[] imageBytes)
    {
        try
        {
            using var inputStream = new MemoryStream(imageBytes);
            using Image image = Image.Load(inputStream);

            // 2x scaling for better quality
            int targetWidth = image.Width * 2;
            int targetHeight = image.Height * 2;
            
            // Apply image enhancements for better OCR
            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.BoxPad
                })
                .Grayscale()                    // Convert to grayscale
                .Contrast(1.2f)                 // Increase contrast
                .BinaryThreshold(0.5f)          // Apply a binary threshold for cleaner text
                .GaussianSharpen(1.0f)          // Sharpen edges
            );
            
            using var outputStream = new MemoryStream();
            image.Save(outputStream, new PngEncoder());
            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enhance image, using original");
            return imageBytes;
        }
    }

    /// <summary>
    /// Extracts text from an image file using OCR with SixLabors enhancement
    /// </summary>
    public async Task<string> ExtractTextFromImage(string imagePath)
    {
        if (!IsAvailable || _tesseractEngine == null)
        {
            throw new InvalidOperationException("OCR service is not available");
        }

        try
        {
            return await Task.Run(() =>
            {
                // Load and enhance an image first
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                byte[] enhancedImageBytes = EnhanceImageForOcr(imageBytes);
                
                // Perform OCR on the enhanced image
                using TesseractImage? img = TesseractImage.LoadFromMemory(enhancedImageBytes);
                using TesseractOCR.Page? page = _tesseractEngine.Process(img);
                return page.Text;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from image: {ImagePath}", imagePath);
            throw;
        }
    }

    /// <summary>
    /// Determines if a PDF is likely scanned by checking text content using PdfPig
    /// </summary>
    public bool IsPdfScanned(string pdfPath, string? password = null)
    {
        try
        {
            var parsingOptions = new ParsingOptions();
            if (!string.IsNullOrEmpty(password))
            {
                parsingOptions.Passwords = [password];
            }

            using PdfDocument pdfDocument = PdfDocument.Open(pdfPath, parsingOptions);
            
            int pagesToCheck = Math.Min(pdfDocument.NumberOfPages, 3); // Check the first 3 pages
            int textlessPages = pdfDocument.GetPages().Take(pagesToCheck).Count(page => !IsTextMeaningful(page.Text));

            // If more than half the checked pages have no meaningful text, likely scanned
            return textlessPages > pagesToCheck / 2;
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

    /// <summary>
    /// Gets the tessdata directory path
    /// </summary>
    private string GetTessdataPath()
    {
        string[] possiblePaths = [
            Path.Combine(Environment.CurrentDirectory, "tessdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tessdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tessdata"),
            @"C:\Program Files\Tesseract-OCR\tessdata",
            @"C:\tesseract\tessdata"
        ];

        foreach (string path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                _logger.LogDebug("Found tessdata directory at: {TessdataPath}", path);
                return path;
            }
        }

        string defaultPath = Path.Combine(Environment.CurrentDirectory, "tessdata");
        _logger.LogWarning("No tessdata directory found, using default: {TessdataPath}", defaultPath);
        return defaultPath;
    }

    public void Dispose()
    {
        _tesseractEngine?.Dispose();
    }
}