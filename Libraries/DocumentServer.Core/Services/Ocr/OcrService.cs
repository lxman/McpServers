using System.Text;
using DocumentServer.Core.Services.Ocr.Models;
using DocumentServer.Rendering;
using Microsoft.Extensions.Logging;
using PdfLibrary.Document;
using PdfLibrary.Structure;

namespace DocumentServer.Core.Services.Ocr;

/// <summary>
/// OCR for PDFs and images.
/// </summary>
/// <remarks>
/// <para>
/// PDF pages are OCR'd by <em>rendering the page</em> and recognising the resulting raster.
/// The obvious alternative - pull the image XObjects out of the page and OCR those - was
/// measured against a real contract corpus and does not work: of 270 image streams on 110
/// scanned pages, 73% could not be decoded as standalone images, because they are JBIG2 or
/// CCITT fax bitstreams that are only meaningful alongside the PDF's /DecodeParms. Separately,
/// 58% of scanned pages carry more than one image, so per-image OCR breaks lines apart even
/// where the bytes do decode.
/// </para>
/// <para>
/// Pages are gated individually. Mixed documents - digital text with scanned exhibits or
/// signature pages - are the normal case here, not an edge case.
/// </para>
/// </remarks>
public sealed class OcrService(
    ILogger<OcrService> logger,
    TesseractCliEngine engine,
    PdfPageRasterizer rasterizer,
    ImagePreprocessor imagePreprocessor) : IDisposable
{
    /// <summary>
    /// Below this many characters, a page's text layer is treated as absent rather than sparse.
    /// </summary>
    private const int MinMeaningfulTextLength = 10;

    /// <summary>
    /// A first-pass result at or below this word count triggers the supersampled retry.
    /// </summary>
    private const int RetryWordThreshold = 5;

    /// <summary>
    /// After this many consecutive pages are rescued by the supersampled retry, stop paying for
    /// the 300 DPI first pass and render supersampled directly.
    /// </summary>
    /// <remarks>
    /// Some documents are bilevel fax scans from end to end, and every page of them aliases at
    /// 300 DPI. Measured on a 96-page Oregon contract, all 96 pages needed the retry, so each
    /// one burned a full render-plus-OCR that was always going to return nothing: 2.7 s/page
    /// against the ~1.2 s/page the engine actually needs. Latching after a short run of
    /// unanimous rescues removes that waste. The latch drops on the first page it mispredicts,
    /// so a document that mixes fax scans with clean pages cannot get stuck in the wrong mode.
    /// </remarks>
    private const int SupersampleLatchThreshold = 3;

    private bool _disposed;

    /// <summary>Whether OCR is available.</summary>
    public bool IsAvailable => engine.IsAvailable;

    /// <summary>
    /// Extracts text from a PDF, OCR'ing only those pages that lack a text layer.
    /// </summary>
    public async Task<OcrResult> ExtractTextFromScannedPdf(string pdfPath, string? password = null)
    {
        if (!IsAvailable)
        {
            return Unavailable();
        }

        try
        {
            logger.LogInformation("Starting OCR extraction from PDF: {PdfPath}", pdfPath);

            return await Task.Run(() => ExtractFromPdfCore(pdfPath, password));
        }
        catch (Exception ex)
        {
            // A failure here means the document could not be read at all - a bad password, a
            // corrupt file, a truncated stream. Report it; never hand back an empty string
            // that would be indexed as a legitimately empty document.
            logger.LogError(ex, "Failed to extract text from PDF: {PdfPath}", pdfPath);
            return new OcrResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private OcrResult ExtractFromPdfCore(string pdfPath, string? password)
    {
        var result = new OcrResult
        {
            Success = true,
            Metadata = new Dictionary<string, string>
            {
                ["FilePath"] = pdfPath,
                ["ProcessedAt"] = DateTime.UtcNow.ToString("O"),
                ["OcrEngine"] = engine.Version ?? "unknown"
            }
        };

        using PdfDocument document = LoadDocument(pdfPath, password);

        int totalPages = document.PageCount;
        result.Metadata["TotalPages"] = totalPages.ToString();
        logger.LogInformation("Processing {PageCount} pages from PDF", totalPages);

        var allText = new StringBuilder();
        var retried = 0;
        var latchedPages = 0;
        var consecutiveRescues = 0;
        var latched = false;

        for (var i = 0; i < totalPages; i++)
        {
            int pageNumber = i + 1;

            try
            {
                PdfPage page = document.GetPage(i)
                    ?? throw new InvalidOperationException($"Page {pageNumber} did not load.");

                string existing = SafeExtractText(page);

                if (IsTextMeaningful(existing))
                {
                    allText.AppendLine(existing);
                    logger.LogDebug("Page {PageNumber} has a text layer, skipping OCR", pageNumber);
                    continue;
                }

                (string text, bool didRetry, bool rescued) = OcrPage(page, pageNumber, latched);
                if (didRetry) retried++;
                if (latched) latchedPages++;

                // Latch on after a run of unanimous rescues; drop it the moment a page
                // disagrees, so a mixed document re-tests the cheap path.
                if (rescued)
                {
                    consecutiveRescues++;
                    if (!latched && consecutiveRescues >= SupersampleLatchThreshold)
                    {
                        latched = true;
                        logger.LogInformation(
                            "{Count} consecutive pages rescued by supersampling; skipping the "
                            + "{Dpi} DPI first pass for the rest of {PdfPath}",
                            consecutiveRescues, PdfPageRasterizer.DefaultDpi, pdfPath);
                    }
                }
                else
                {
                    if (latched)
                    {
                        logger.LogInformation(
                            "Page {PageNumber} did not need supersampling; releasing the latch",
                            pageNumber);
                    }

                    consecutiveRescues = 0;
                    latched = false;
                }

                allText.AppendLine(text);
                result.PagesProcessed++;
            }
            catch (Exception ex)
            {
                // One unreadable page must not abort the document, but it must stay visible:
                // counted, warned, and marked inline so a silently short extraction cannot
                // pass for a complete one.
                logger.LogWarning(ex, "Failed to process page {PageNumber} of {PdfPath}",
                    pageNumber, pdfPath);
                allText.AppendLine($"[OCR Error on page {pageNumber}: {ex.Message}]");
                result.PagesWithErrors++;
                result.Warnings.Add($"Page {pageNumber}: {ex.Message}");
            }
        }

        result.ExtractedText = allText.ToString();
        result.Metadata["ExtractedLength"] = result.ExtractedText.Length.ToString();
        result.Metadata["PagesSupersampleRetried"] = retried.ToString();
        result.Metadata["PagesSupersampleLatched"] = latchedPages.ToString();

        logger.LogInformation(
            "Completed OCR of {PdfPath}. OCR'd: {Processed}, retried: {Retried}, "
            + "latched: {Latched}, errors: {Errors}",
            pdfPath, result.PagesProcessed, retried, latchedPages, result.PagesWithErrors);

        return result;
    }

    /// <summary>
    /// OCRs one page: render at 300 DPI, and retry supersampled if that returns almost nothing.
    /// </summary>
    /// <remarks>
    /// A single global DPI is wrong for scanned contracts. 150 DPI rescues bilevel fax pages
    /// but costs around 9% of the words on already-good pages; 300 DPI alone leaves a handful
    /// of pages returning literally zero bytes with no error. Measured over 110 scanned pages,
    /// 300 DPI plus a supersampled retry recovered the most words of any strategy tried, with
    /// no page made worse. The retry fires on roughly 10% of pages, so it costs very little.
    /// </remarks>
    private (string Text, bool DidRetry, bool Rescued) OcrPage(
        PdfPage page, int pageNumber, bool preferSupersample)
    {
        if (preferSupersample)
        {
            byte[] latchedRaster = rasterizer.RenderPage(
                page, pageNumber, PdfPageRasterizer.RetryDpi, PdfPageRasterizer.RetrySupersample);
            (string latchedText, _) = engine.Recognize(latchedRaster);

            if (CountWords(latchedText) > RetryWordThreshold)
            {
                return (latchedText, true, true);
            }

            // The latch mispredicted this page. Fall through to the standard two-pass so the
            // page still gets its ordinary chance rather than being written off.
            logger.LogDebug(
                "Page {PageNumber}: latched supersample returned almost nothing, falling back",
                pageNumber);
        }

        byte[] raster = rasterizer.RenderPage(page, pageNumber, PdfPageRasterizer.DefaultDpi);
        (string text, _) = engine.Recognize(raster);

        int words = CountWords(text);
        if (words > RetryWordThreshold)
        {
            return (text, false, false);
        }

        logger.LogDebug(
            "Page {PageNumber} returned {Words} words at {Dpi} DPI; retrying supersampled",
            pageNumber, words, PdfPageRasterizer.DefaultDpi);

        byte[] retryRaster = rasterizer.RenderPage(
            page, pageNumber, PdfPageRasterizer.RetryDpi, PdfPageRasterizer.RetrySupersample);
        (string retryText, _) = engine.Recognize(retryRaster);

        // Keep whichever pass read more. The retry exists to rescue aliased pages, not to
        // overwrite a good result on a page that is genuinely near-blank.
        int retryWords = CountWords(retryText);
        if (retryWords > words)
        {
            logger.LogInformation(
                "Page {PageNumber} recovered by supersampled retry: {Before} -> {After} words",
                pageNumber, words, retryWords);
            return (retryText, true, true);
        }

        return (text, true, false);
    }

    /// <summary>
    /// Extracts text from an image file using OCR.
    /// </summary>
    public async Task<OcrResult> ExtractTextFromImage(string imagePath, bool enhanceImage = true)
    {
        if (!IsAvailable)
        {
            return Unavailable();
        }

        try
        {
            logger.LogInformation("Starting OCR extraction from image: {ImagePath}", imagePath);

            OcrResult result = await Task.Run(() =>
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);

                if (enhanceImage)
                {
                    imageBytes = imagePreprocessor.EnhanceImageForOcr(imageBytes);
                }

                (string text, float? confidence) = engine.Recognize(imageBytes, withConfidence: true);

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
                        ["OcrEngine"] = engine.Version ?? "unknown",
                        ["ExtractedLength"] = text.Length.ToString()
                    }
                };
            });

            logger.LogInformation("Completed OCR of image. Confidence: {Confidence:P1}",
                result.Confidence ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract text from image: {ImagePath}", imagePath);
            return new OcrResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Reports which pages of a PDF lack a text layer.
    /// </summary>
    /// <remarks>
    /// Every page is examined. Sampling the first few pages and generalising is unsafe here:
    /// scanned material is routinely appended as exhibits or signature pages, so a prefix
    /// sample reports "has text" for documents that are half images.
    /// </remarks>
    public PdfScanAnalysis AnalyzePdf(string pdfPath, string? password = null)
    {
        using PdfDocument document = LoadDocument(pdfPath, password);

        var analysis = new PdfScanAnalysis { TotalPages = document.PageCount };

        for (var i = 0; i < document.PageCount; i++)
        {
            PdfPage? page = document.GetPage(i);
            string text = page is null ? string.Empty : SafeExtractText(page);
            bool requiresOcr = !IsTextMeaningful(text);

            analysis.Pages.Add(new PageScanInfo
            {
                PageNumber = i + 1,
                TextLength = text.Trim().Length,
                RequiresOcr = requiresOcr
            });

            if (requiresOcr) analysis.PagesRequiringOcr++;
            else analysis.PagesWithText++;
        }

        logger.LogInformation(
            "Scan analysis of {PdfPath}: {Ocr}/{Total} pages need OCR (isScanned={IsScanned})",
            pdfPath, analysis.PagesRequiringOcr, analysis.TotalPages, analysis.IsScanned);

        return analysis;
    }

    /// <summary>
    /// Whether a PDF is predominantly scanned.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="AnalyzePdf"/>. This answers a descriptive question, and a document can
    /// return false here while still containing hundreds of pages that need OCR.
    /// </remarks>
    public bool IsPdfScanned(string pdfPath, string? password = null)
    {
        try
        {
            return AnalyzePdf(pdfPath, password).IsScanned;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to analyze PDF for scanned content: {PdfPath}", pdfPath);
            return false;
        }
    }

    private static PdfDocument LoadDocument(string pdfPath, string? password)
        => string.IsNullOrEmpty(password)
            ? PdfDocument.Load(pdfPath)
            : PdfDocument.Load(pdfPath, password);

    private string SafeExtractText(PdfPage page)
    {
        try
        {
            return page.ExtractText() ?? string.Empty;
        }
        catch (Exception ex)
        {
            // A page whose text layer will not parse is treated as needing OCR, which is the
            // safe direction: it gets rendered and recognised rather than dropped.
            logger.LogDebug(ex, "Text-layer extraction failed; page will be treated as scanned");
            return string.Empty;
        }
    }

    private OcrResult Unavailable()
    {
        logger.LogError("OCR requested but no usable Tesseract executable is available");
        return new OcrResult
        {
            Success = false,
            ErrorMessage = "OCR service is not available: no usable Tesseract executable was found."
        };
    }

    private static int CountWords(string text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static bool IsTextMeaningful(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        string clean = text.Trim();
        if (clean.Length < MinMeaningfulTextLength) return false;

        int readable = clean.Count(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || c == ' ');
        return (double)readable / clean.Length > 0.7;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        logger.LogDebug("OCR service disposed");
    }
}
