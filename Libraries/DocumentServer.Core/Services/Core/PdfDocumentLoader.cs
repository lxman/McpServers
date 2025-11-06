using System.Diagnostics;
using System.Text;
using DocumentServer.Core.Models.Common;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocumentServer.Core.Services.Core;

/// <summary>
/// Document loader for PDF files
/// </summary>
public class PdfDocumentLoader : IDocumentLoader
{
    private readonly ILogger<PdfDocumentLoader> _logger;
    private readonly PasswordManager _passwordManager;

    private static readonly string[] SupportedExtensions = [".pdf"];

    /// <summary>
    /// Initializes a new instance of the PdfDocumentLoader
    /// </summary>
    public PdfDocumentLoader(ILogger<PdfDocumentLoader> logger, PasswordManager passwordManager)
    {
        _logger = logger;
        _passwordManager = passwordManager;
        _logger.LogInformation("PdfDocumentLoader initialized");
    }

    /// <inheritdoc />
    public DocumentType SupportedType => DocumentType.Pdf;

    /// <inheritdoc />
    public bool CanLoad(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<LoadedDocument>> LoadAsync(string filePath, string? password = null)
    {
        _logger.LogInformation("Loading PDF document: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("PDF file not found: {FilePath}", filePath);
                return ServiceResult<LoadedDocument>.CreateFailure("File not found");
            }

            var fileInfo = new FileInfo(filePath);
            
            // Try to get password from manager if not provided
            password ??= _passwordManager.GetPasswordForFile(filePath);

            // Open the PDF document
            PdfDocument pdfDocument;
            try
            {
                pdfDocument = string.IsNullOrEmpty(password)
                    ? PdfDocument.Open(filePath)
                    : PdfDocument.Open(filePath, new ParsingOptions { Password = password });
                
                _logger.LogDebug("PDF opened successfully: {FilePath}, Pages={PageCount}", 
                    filePath, pdfDocument.NumberOfPages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open PDF: {FilePath}", filePath);
                
                // Check if it's a password issue
                if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult<LoadedDocument>.CreateFailure(
                        "PDF is encrypted and requires a password");
                }
                
                return ServiceResult<LoadedDocument>.CreateFailure($"Failed to open PDF: {ex.Message}");
            }

            // Extract basic metadata
            Dictionary<string, string> metadata = ExtractMetadata(pdfDocument, filePath);
            
            // Calculate estimated memory size
            long estimatedMemorySize = EstimateMemorySize(fileInfo.Length, pdfDocument.NumberOfPages);

            var loadedDocument = new LoadedDocument
            {
                FilePath = filePath,
                DocumentType = DocumentType.Pdf,
                LoadedAt = DateTime.UtcNow,
                DocumentObject = pdfDocument,
                MemorySizeBytes = estimatedMemorySize,
                WasPasswordProtected = !string.IsNullOrEmpty(password),
                AccessCount = 0,
                LastAccessedAt = DateTime.UtcNow
            };

            _logger.LogInformation("PDF loaded successfully: {FilePath}, Pages={PageCount}, Size={SizeKB}KB",
                filePath, pdfDocument.NumberOfPages, estimatedMemorySize / 1024);

            return ServiceResult<LoadedDocument>.CreateSuccess(loadedDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading PDF: {FilePath}", filePath);
            return ServiceResult<LoadedDocument>.CreateFailure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<string>> ExtractTextAsync(LoadedDocument document)
    {
        _logger.LogInformation("Extracting text from PDF: {FilePath}", document.FilePath);

        try
        {
            if (document.DocumentObject is not PdfDocument pdfDocument)
            {
                _logger.LogError("Invalid document object type for PDF extraction: {FilePath}", document.FilePath);
                return ServiceResult<string>.CreateFailure("Invalid document object");
            }

            var textBuilder = new StringBuilder();
            
            foreach (Page page in pdfDocument.GetPages())
            {
                try
                {
                    string pageText = ContentOrderTextExtractor.GetText(page);
                    textBuilder.AppendLine($"--- Page {page.Number} ---");
                    textBuilder.AppendLine(pageText);
                    textBuilder.AppendLine();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract text from page {PageNumber} in: {FilePath}",
                        page.Number, document.FilePath);
                    textBuilder.AppendLine($"[Error extracting page {page.Number}]");
                }
            }

            var extractedText = textBuilder.ToString();
            
            _logger.LogInformation("Text extraction complete: {FilePath}, Length={Length} characters",
                document.FilePath, extractedText.Length);

            return await Task.FromResult(ServiceResult<string>.CreateSuccess(extractedText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF: {FilePath}", document.FilePath);
            return ServiceResult<string>.CreateFailure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<DocumentInfo>> GetDocumentInfoAsync(string filePath, string? password = null)
    {
        _logger.LogInformation("Getting document info for PDF: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                return ServiceResult<DocumentInfo>.CreateFailure("File not found");
            }

            var fileInfo = new FileInfo(filePath);
            password ??= _passwordManager.GetPasswordForFile(filePath);

            PdfDocument pdfDocument;
            try
            {
                pdfDocument = string.IsNullOrEmpty(password)
                    ? PdfDocument.Open(filePath)
                    : PdfDocument.Open(filePath, new ParsingOptions { Password = password });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open PDF for info extraction: {FilePath}", filePath);
                
                // Return basic file info even if we can't open the PDF
                return ServiceResult<DocumentInfo>.CreateSuccess(new DocumentInfo
                {
                    FilePath = filePath,
                    DocumentType = DocumentType.Pdf,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    IsEncrypted = true
                });
            }

            using (pdfDocument)
            {
                var docInfo = new DocumentInfo
                {
                    FilePath = filePath,
                    DocumentType = DocumentType.Pdf,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    IsEncrypted = !string.IsNullOrEmpty(password),
                    PageCount = pdfDocument.NumberOfPages,
                    Author = pdfDocument.Information?.Author,
                    Title = pdfDocument.Information?.Title,
                    CreatedDate = ParsePdfDate(pdfDocument.Information?.CreationDate),
                    Metadata = new Dictionary<string, string>
                    {
                        ["Subject"] = pdfDocument.Information?.Subject ?? string.Empty,
                        ["Keywords"] = pdfDocument.Information?.Keywords ?? string.Empty,
                        ["Creator"] = pdfDocument.Information?.Creator ?? string.Empty,
                        ["Producer"] = pdfDocument.Information?.Producer ?? string.Empty,
                        ["Version"] = pdfDocument.Version.ToString(),
                        ["ModificationDate"] = ParsePdfDate(pdfDocument.Information?.ModifiedDate)?.ToString() ?? string.Empty
                    }
                };

                _logger.LogInformation("Document info extracted: {FilePath}, Pages={PageCount}",
                    filePath, docInfo.PageCount);

                return await Task.FromResult(ServiceResult<DocumentInfo>.CreateSuccess(docInfo));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document info: {FilePath}", filePath);
            return ServiceResult<DocumentInfo>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract metadata from a PDF document
    /// </summary>
    private Dictionary<string, string> ExtractMetadata(PdfDocument pdfDocument, string filePath)
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            DocumentInformation? info = pdfDocument.Information;
            
            metadata["Title"] = info?.Title ?? Path.GetFileNameWithoutExtension(filePath);
            metadata["Author"] = info?.Author ?? string.Empty;
            metadata["Subject"] = info?.Subject ?? string.Empty;
            metadata["Keywords"] = info?.Keywords ?? string.Empty;
            metadata["Creator"] = info?.Creator ?? string.Empty;
            metadata["Producer"] = info?.Producer ?? string.Empty;
            metadata["Version"] = pdfDocument.Version.ToString();
            metadata["PageCount"] = pdfDocument.NumberOfPages.ToString();
            
            DateTime? creationDate = ParsePdfDate(info?.CreationDate);
            if (creationDate.HasValue)
            {
                metadata["CreationDate"] = creationDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            }
            
            DateTime? modDate = ParsePdfDate(info?.ModifiedDate);
            if (modDate.HasValue)
            {
                metadata["ModificationDate"] = modDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            }

            _logger.LogDebug("Metadata extracted successfully: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting PDF metadata: {FilePath}", filePath);
        }

        return metadata;
    }

    /// <summary>
    /// Estimate memory size of a loaded PDF document
    /// </summary>
    private static long EstimateMemorySize(long fileSize, int pageCount)
    {
        // Rough estimation: file size + overhead per page (images, objects, etc.)
        // PDF files in memory typically use 1.5-3x their disk size
        long baseMemory = fileSize * 2;
        long pageOverhead = pageCount * 50_000; // ~50KB per page for structures
        
        return baseMemory + pageOverhead;
    }

    /// <summary>
    /// Parse PDF date string to DateTime
    /// </summary>
    private DateTime? ParsePdfDate(string? pdfDate)
    {
        if (string.IsNullOrEmpty(pdfDate))
            return null;

        try
        {
            // PDF dates are in format: D:YYYYMMDDHHmmSSOHH'mm
            if (pdfDate.StartsWith("D:"))
            {
                string dateStr = pdfDate[2..]; // Remove "D:" prefix
                
                // Handle timezone offset
                int timezoneIndex = dateStr.IndexOfAny(['+', '-']);
                if (timezoneIndex > 0)
                {
                    dateStr = dateStr[..timezoneIndex];
                }
                
                // Pad to ensure we have all components
                dateStr = dateStr.PadRight(14, '0');
                
                if (dateStr.Length >= 14 &&
                    int.TryParse(dateStr[..4], out int year) &&
                    int.TryParse(dateStr[4..6], out int month) &&
                    int.TryParse(dateStr[6..8], out int day) &&
                    int.TryParse(dateStr[8..10], out int hour) &&
                    int.TryParse(dateStr[10..12], out int minute) &&
                    int.TryParse(dateStr[12..14], out int second))
                {
                    if (year is >= 1900 and <= 9999 &&
                        month is >= 1 and <= 12 &&
                        day is >= 1 and <= 31 &&
                        hour is >= 0 and <= 23 &&
                        minute is >= 0 and <= 59 &&
                        second is >= 0 and <= 59)
                    {
                        try
                        {
                            return new DateTime(year, month, day, hour, minute, second);
                        }
                        catch
                        {
                            // Invalid date combination
                            return null;
                        }
                    }
                }
            }
            
            // Fallback: try standard DateTime parsing
            if (DateTime.TryParse(pdfDate, out DateTime result))
                return result;
        }
        catch (Exception ex)
        {
            // Log but don't fail
            Debug.WriteLine($"Failed to parse PDF date: {pdfDate} - {ex.Message}");
        }

        return null;
    }
}
