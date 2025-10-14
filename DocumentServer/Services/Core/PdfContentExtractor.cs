using System.Text;
using DocumentServer.Models.Common;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocumentServer.Services.Core;

/// <summary>
/// Content extractor for PDF documents
/// </summary>
public class PdfContentExtractor : IContentExtractor
{
    private readonly ILogger<PdfContentExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the PdfContentExtractor
    /// </summary>
    public PdfContentExtractor(ILogger<PdfContentExtractor> logger)
    {
        _logger = logger;
        _logger.LogInformation("PdfContentExtractor initialized");
    }

    /// <inheritdoc />
    public DocumentType SupportedType => DocumentType.Pdf;

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
            var pageCount = 0;

            foreach (Page page in pdfDocument.GetPages())
            {
                try
                {
                    string pageText = ContentOrderTextExtractor.GetText(page);
                    textBuilder.AppendLine($"--- Page {page.Number} ---");
                    textBuilder.AppendLine(pageText);
                    textBuilder.AppendLine();
                    pageCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract text from page {PageNumber} in: {FilePath}",
                        page.Number, document.FilePath);
                    textBuilder.AppendLine($"[Error extracting page {page.Number}]");
                }
            }

            var extractedText = textBuilder.ToString();

            _logger.LogInformation("Text extraction complete: {FilePath}, Pages={PageCount}, Length={Length} characters",
                document.FilePath, pageCount, extractedText.Length);

            return await Task.FromResult(ServiceResult<string>.CreateSuccess(extractedText));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF: {FilePath}", document.FilePath);
            return ServiceResult<string>.CreateFailure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Dictionary<string, string>>> ExtractMetadataAsync(LoadedDocument document)
    {
        _logger.LogInformation("Extracting metadata from PDF: {FilePath}", document.FilePath);

        try
        {
            if (document.DocumentObject is not PdfDocument pdfDocument)
            {
                return ServiceResult<Dictionary<string, string>>.CreateFailure("Invalid document object");
            }

            var metadata = new Dictionary<string, string>();
            DocumentInformation? info = pdfDocument.Information;

            metadata["Title"] = info?.Title ?? string.Empty;
            metadata["Author"] = info?.Author ?? string.Empty;
            metadata["Subject"] = info?.Subject ?? string.Empty;
            metadata["Keywords"] = info?.Keywords ?? string.Empty;
            metadata["Creator"] = info?.Creator ?? string.Empty;
            metadata["Producer"] = info?.Producer ?? string.Empty;
            metadata["Version"] = pdfDocument.Version.ToString();
            metadata["PageCount"] = pdfDocument.NumberOfPages.ToString();

            // Parse dates
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

            // Calculate document statistics
            var totalImages = 0;
            long totalTextLength = 0;

            foreach (Page page in pdfDocument.GetPages())
            {
                totalImages += page.GetImages().Count();
                totalTextLength += ContentOrderTextExtractor.GetText(page).Length;
            }

            metadata["TotalImages"] = totalImages.ToString();
            metadata["TotalTextLength"] = totalTextLength.ToString();
            metadata["AverageTextLengthPerPage"] = (totalTextLength / pdfDocument.NumberOfPages).ToString();

            _logger.LogInformation("Metadata extraction complete: {FilePath}, Fields={Count}",
                document.FilePath, metadata.Count);

            return await Task.FromResult(ServiceResult<Dictionary<string, string>>.CreateSuccess(metadata));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from PDF: {FilePath}", document.FilePath);
            return ServiceResult<Dictionary<string, string>>.CreateFailure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<object>> ExtractStructuredContentAsync(LoadedDocument document)
    {
        _logger.LogInformation("Extracting structured content from PDF: {FilePath}", document.FilePath);

        try
        {
            if (document.DocumentObject is not PdfDocument pdfDocument)
            {
                return ServiceResult<object>.CreateFailure("Invalid document object");
            }

            var pages = new List<Dictionary<string, object>>();

            foreach (Page page in pdfDocument.GetPages())
            {
                var pageData = new Dictionary<string, object>
                {
                    ["PageNumber"] = page.Number,
                    ["Width"] = page.Width,
                    ["Height"] = page.Height,
                    ["Rotation"] = page.Rotation.Value,
                    ["Text"] = ContentOrderTextExtractor.GetText(page)
                };

                // Extract images
                var images = new List<Dictionary<string, object>>();
                var imageIndex = 0;
                foreach (IPdfImage image in page.GetImages())
                {
                    images.Add(new Dictionary<string, object>
                    {
                        ["Index"] = imageIndex++,
                        ["Width"] = (int)image.Bounds.Width,
                        ["Height"] = (int)image.Bounds.Height,
                        ["X"] = image.Bounds.Left,
                        ["Y"] = image.Bounds.Bottom,
                        ["SizeBytes"] = image.RawBytes.Length
                    });
                }
                pageData["Images"] = images;
                pageData["ImageCount"] = images.Count;

                // Extract words for detailed analysis
                var words = page.GetWords().Take(100).Select(w => new
                {
                    w.Text,
                    X = w.BoundingBox.Left,
                    Y = w.BoundingBox.Bottom
                }).ToList();
                pageData["SampleWords"] = words;
                pageData["TotalWords"] = page.GetWords().Count();

                pages.Add(pageData);
            }

            var structuredContent = new Dictionary<string, object>
            {
                ["DocumentInfo"] = new
                {
                    PageCount = pdfDocument.NumberOfPages,
                    Version = pdfDocument.Version.ToString()
                },
                ["Pages"] = pages
            };

            _logger.LogInformation("Structured content extraction complete: {FilePath}, Pages={PageCount}",
                document.FilePath, pages.Count);

            return await Task.FromResult(ServiceResult<object>.CreateSuccess(structuredContent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting structured content from PDF: {FilePath}", document.FilePath);
            return ServiceResult<object>.CreateFailure(ex);
        }
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
                string dateStr = pdfDate[2..];

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
                            return null;
                        }
                    }
                }
            }

            // Fallback: try standard DateTime parsing
            if (DateTime.TryParse(pdfDate, out DateTime result))
                return result;
        }
        catch
        {
            // Parsing failed, return null
        }

        return null;
    }
}
