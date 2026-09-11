namespace DocumentServer.Core.Services.Ocr.Models;

/// <summary>
/// Per-page text-layer status for a PDF.
/// </summary>
public class PageScanInfo
{
    /// <summary>1-based page number.</summary>
    public int PageNumber { get; set; }

    /// <summary>Characters of usable text extracted from the page's text layer.</summary>
    public int TextLength { get; set; }

    /// <summary>True when this page has no usable text layer and must be rasterised and OCR'd.</summary>
    public bool RequiresOcr { get; set; }
}
