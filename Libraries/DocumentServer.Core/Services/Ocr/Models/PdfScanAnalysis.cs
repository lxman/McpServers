namespace DocumentServer.Core.Services.Ocr.Models;

/// <summary>
/// Result of analysing which pages of a PDF carry a usable text layer.
/// </summary>
/// <remarks>
/// <b><see cref="IsScanned"/> and <see cref="RequiresOcr"/> are not the same question, and
/// conflating them loses pages.</b> Real contract PDFs are frequently mixed: a digital-native
/// document with scanned exhibits, amendments or signature pages appended. One corpus document
/// measured 216 scanned pages out of 454 - it is not "a scanned document" by any reasonable
/// description, but skipping OCR on it silently drops 216 pages, and the document still returns
/// search results from the half that did index, so nothing looks broken.
/// Gate OCR on <see cref="RequiresOcr"/>; use <see cref="IsScanned"/> only for description.
/// </remarks>
public class PdfScanAnalysis
{
    /// <summary>Total pages in the document.</summary>
    public int TotalPages { get; set; }

    /// <summary>Pages with a usable text layer.</summary>
    public int PagesWithText { get; set; }

    /// <summary>Pages with no usable text layer, which therefore need OCR.</summary>
    public int PagesRequiringOcr { get; set; }

    /// <summary>
    /// True when most pages lack a text layer. A description of the document, not an
    /// instruction: a document can be predominantly digital and still need OCR.
    /// </summary>
    public bool IsScanned => TotalPages > 0 && PagesRequiringOcr > TotalPages / 2;

    /// <summary>
    /// True when <em>any</em> page lacks a text layer. This is the flag that should gate OCR.
    /// </summary>
    public bool RequiresOcr => PagesRequiringOcr > 0;

    /// <summary>Per-page detail.</summary>
    public List<PageScanInfo> Pages { get; set; } = [];
}
