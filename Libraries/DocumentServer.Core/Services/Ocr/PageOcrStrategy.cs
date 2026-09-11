using DocumentServer.Rendering;
using Microsoft.Extensions.Logging;
using PdfLibrary.Document;

namespace DocumentServer.Core.Services.Ocr;

/// <summary>Outcome of recognising a single rendered page.</summary>
/// <param name="Text">Recognised text.</param>
/// <param name="Words">Word count of <paramref name="Text"/>.</param>
/// <param name="Retried">Whether the supersampled pass ran.</param>
/// <param name="Rescued">Whether the supersampled pass read more than the first pass.</param>
public readonly record struct PageOcrOutcome(string Text, int Words, bool Retried, bool Rescued);

/// <summary>
/// Renders and recognises PDF pages, with the retry and latch behaviour the corpus needs.
/// </summary>
/// <remarks>
/// <para>
/// A single global DPI is wrong for scanned contracts. 150 DPI rescues bilevel fax pages but
/// costs around 9% of the words on already-good pages; 300 DPI alone leaves a handful of pages
/// returning literally zero bytes with no error. Measured over 110 scanned pages, 300 DPI plus
/// a supersampled retry recovered the most words of any strategy tried, with no page made
/// worse. The retry fires on roughly 10% of pages, so it costs very little.
/// </para>
/// <para>
/// This is a separate class because the policy is stateful - the latch below spans pages - and
/// because more than one caller needs it. Duplicating it into each caller is how the two
/// copies drift, and a drifted copy still produces plausible text, so nothing would fail.
/// </para>
/// <para>
/// Instances are single-threaded and hold per-document state; use one per document and do not
/// share one across threads.
/// </para>
/// </remarks>
public sealed class PageOcrStrategy(
    ILogger<PageOcrStrategy> logger,
    TesseractCliEngine engine,
    PdfPageRasterizer rasterizer)
{
    /// <summary>A result at or below this word count triggers the supersampled retry.</summary>
    public const int RetryWordThreshold = 5;

    /// <summary>
    /// After this many consecutive pages are rescued by the retry, stop paying for the 300 DPI
    /// first pass and render supersampled directly.
    /// </summary>
    /// <remarks>
    /// Some documents are bilevel fax scans from end to end, and every page of them aliases at
    /// 300 DPI. Measured on a 96-page Oregon contract, all 96 pages needed the retry, so each
    /// one burned a full render-plus-OCR that was always going to return nothing. Latching
    /// after a short run of unanimous rescues removes that waste. The latch drops on the first
    /// page it mispredicts, so a document that mixes fax scans with clean pages cannot get
    /// stuck in the wrong mode.
    /// </remarks>
    public const int SupersampleLatchThreshold = 3;

    private int _consecutiveRescues;
    private bool _latched;

    /// <summary>Pages recognised through the latched supersample path.</summary>
    public int LatchedPages { get; private set; }

    /// <summary>Pages on which the supersampled pass ran at all.</summary>
    public int RetriedPages { get; private set; }

    /// <summary>Clears per-document latch state. Call between documents.</summary>
    public void Reset()
    {
        _consecutiveRescues = 0;
        _latched = false;
        LatchedPages = 0;
        RetriedPages = 0;
    }

    /// <summary>Renders and recognises one page, applying the retry and latch policy.</summary>
    public PageOcrOutcome OcrPage(PdfPage page, int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(page);

        bool wasLatched = _latched;
        PageOcrOutcome outcome = Recognize(page, pageNumber, wasLatched);

        if (outcome.Retried) RetriedPages++;
        if (wasLatched) LatchedPages++;

        UpdateLatch(outcome.Rescued, pageNumber);
        return outcome;
    }

    private PageOcrOutcome Recognize(PdfPage page, int pageNumber, bool preferSupersample)
    {
        if (preferSupersample)
        {
            string latchedText = RecognizeSupersampled(page, pageNumber);
            int latchedWords = CountWords(latchedText);

            if (latchedWords > RetryWordThreshold)
            {
                return new PageOcrOutcome(latchedText, latchedWords, true, true);
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
            return new PageOcrOutcome(text, words, false, false);
        }

        logger.LogDebug(
            "Page {PageNumber} returned {Words} words at {Dpi} DPI; retrying supersampled",
            pageNumber, words, PdfPageRasterizer.DefaultDpi);

        string retryText = RecognizeSupersampled(page, pageNumber);
        int retryWords = CountWords(retryText);

        // Keep whichever pass read more. The retry exists to rescue aliased pages, not to
        // overwrite a good result on a page that is genuinely near-blank.
        if (retryWords > words)
        {
            logger.LogInformation(
                "Page {PageNumber} recovered by supersampled retry: {Before} -> {After} words",
                pageNumber, words, retryWords);
            return new PageOcrOutcome(retryText, retryWords, true, true);
        }

        return new PageOcrOutcome(text, words, true, false);
    }

    private string RecognizeSupersampled(PdfPage page, int pageNumber)
    {
        byte[] raster = rasterizer.RenderPage(
            page, pageNumber, PdfPageRasterizer.RetryDpi, PdfPageRasterizer.RetrySupersample);
        (string text, _) = engine.Recognize(raster);
        return text;
    }

    private void UpdateLatch(bool rescued, int pageNumber)
    {
        if (rescued)
        {
            _consecutiveRescues++;
            if (!_latched && _consecutiveRescues >= SupersampleLatchThreshold)
            {
                _latched = true;
                logger.LogInformation(
                    "{Count} consecutive pages rescued by supersampling; skipping the {Dpi} DPI "
                    + "first pass for the rest of this document",
                    _consecutiveRescues, PdfPageRasterizer.DefaultDpi);
            }

            return;
        }

        if (_latched)
        {
            logger.LogInformation(
                "Page {PageNumber} did not need supersampling; releasing the latch", pageNumber);
        }

        _consecutiveRescues = 0;
        _latched = false;
    }

    private static int CountWords(string text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
