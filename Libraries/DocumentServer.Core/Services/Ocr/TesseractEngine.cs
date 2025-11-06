using Microsoft.Extensions.Logging;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractImage = TesseractOCR.Pix.Image;

namespace DocumentServer.Core.Services.Ocr;

/// <summary>
/// Wrapper around Tesseract OCR engine for text extraction
/// </summary>
public class TesseractEngine : IDisposable
{
    private readonly ILogger<TesseractEngine> _logger;
    private readonly Engine? _engine;
    private bool _disposed;

    /// <summary>
    /// Indicates whether the OCR engine is available and ready to use
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Path to the tessdata directory
    /// </summary>
    public string? TessdataPath { get; }

    public TesseractEngine(ILogger<TesseractEngine> logger)
    {
        _logger = logger;

        try
        {
            TessdataPath = FindTessdataPath();
            
            if (TessdataPath is not null && Directory.Exists(TessdataPath))
            {
                _logger.LogInformation("Initializing Tesseract OCR engine with tessdata path: {TessdataPath}", TessdataPath);
                _engine = new Engine(TessdataPath, Language.English);
                IsAvailable = true;
                _logger.LogInformation("Tesseract OCR engine initialized successfully");
            }
            else
            {
                _logger.LogWarning("Tessdata directory not found. OCR features will be disabled.");
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
    /// Extract text from image bytes
    /// </summary>
    /// <param name="imageBytes">Image data to process</param>
    /// <returns>Extracted text</returns>
    public string ExtractText(byte[] imageBytes)
    {
        ThrowIfNotAvailable();

        try
        {
            _logger.LogDebug("Extracting text from image ({Size} bytes)", imageBytes.Length);

            using TesseractImage? img = TesseractImage.LoadFromMemory(imageBytes);
            using Page? page = _engine!.Process(img);
            
            string text = page.Text;
            _logger.LogDebug("Extracted {Length} characters from image", text?.Length ?? 0);

            return text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from image");
            throw;
        }
    }

    /// <summary>
    /// Extract text from image bytes with confidence information
    /// </summary>
    /// <param name="imageBytes">Image data to process</param>
    /// <returns>Tuple of extracted text and confidence score (0.0 to 1.0)</returns>
    public (string Text, float Confidence) ExtractTextWithConfidence(byte[] imageBytes)
    {
        ThrowIfNotAvailable();

        try
        {
            _logger.LogDebug("Extracting text with confidence from image ({Size} bytes)", imageBytes.Length);

            using TesseractImage? img = TesseractImage.LoadFromMemory(imageBytes);
            using Page? page = _engine!.Process(img);
            
            string text = page.Text ?? string.Empty;
            float confidence = page.MeanConfidence / 100f; // Convert to 0.0-1.0 range

            _logger.LogDebug("Extracted {Length} characters with {Confidence:P1} confidence", 
                text.Length, confidence);

            return (text, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text with confidence from image");
            throw;
        }
    }

    /// <summary>
    /// Extract text from multiple images
    /// </summary>
    /// <param name="images">Collection of image byte arrays</param>
    /// <returns>Combined extracted text</returns>
    public async Task<string> ExtractTextFromMultipleImages(IEnumerable<byte[]> images)
    {
        ThrowIfNotAvailable();

        var results = new List<string>();

        await Task.Run(() =>
        {
            foreach (byte[] imageBytes in images)
            {
                try
                {
                    string text = ExtractText(imageBytes);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        results.Add(text);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract text from one of the images, continuing with others");
                }
            }
        });

        return string.Join(Environment.NewLine, results);
    }

    /// <summary>
    /// Finds the tessdata directory path
    /// </summary>
    private string? FindTessdataPath()
    {
        string[] possiblePaths =
        [
            Path.Combine(Environment.CurrentDirectory, "tessdata"),
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
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

        _logger.LogWarning("No tessdata directory found in any expected location");
        return null;
    }

    /// <summary>
    /// Throws if the OCR engine is not available
    /// </summary>
    private void ThrowIfNotAvailable()
    {
        if (!IsAvailable || _engine is null)
        {
            throw new InvalidOperationException("OCR service is not available. Tesseract engine not initialized.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _engine?.Dispose();
        _disposed = true;
        _logger.LogDebug("Tesseract engine disposed");
    }
}
