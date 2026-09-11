using Microsoft.Extensions.Logging;
using PdfLibrary.Document;
using PdfLibrary.Rendering.SkiaSharp;
using SkiaSharp;

namespace DocumentServer.Rendering;

/// <summary>
/// Rasterises PDF pages for OCR.
/// </summary>
/// <remarks>
/// <para>
/// This exists because OCR must be run against a <em>rendered page</em>, not against the
/// image XObjects embedded in that page. Extracting embedded images and handing their raw
/// bytes to a decoder is a tempting shortcut that does not work on real scans: measured over
/// 270 image streams on 110 scanned contract pages, 73% of them were undecodable as
/// standalone images (37% JBIG2, 31% CCITT fax, 14% Flate-wrapped JPEG). Those are
/// compressed bitstreams that are only meaningful together with the PDF's /DecodeParms, not
/// image files. On top of that, 58% of scanned pages carry more than one image, so per-image
/// OCR shreds line continuity even where the bytes happen to decode.
/// </para>
/// <para>
/// Rendering delegates decoding, colour-space resolution, MRC compositing and /Rotate to the
/// PDF engine, which handles all of those codecs, and yields one raster per page.
/// </para>
/// </remarks>
public sealed class PdfPageRasterizer(ILogger<PdfPageRasterizer> logger)
{
    /// <summary>Primary render resolution.</summary>
    public const double DefaultDpi = 300.0;

    /// <summary>Resolution for the supersampled retry. Lower on purpose - see <see cref="RenderSupersampled"/>.</summary>
    public const double RetryDpi = 150.0;

    /// <summary>Supersample factor for the retry pass.</summary>
    public const int RetrySupersample = 6;

    /// <summary>
    /// Renders a page to PNG bytes.
    /// </summary>
    /// <param name="page">Page to render.</param>
    /// <param name="pageNumber">1-based page number, for diagnostics.</param>
    /// <param name="dpi">Target resolution of the returned raster.</param>
    /// <param name="supersample">
    /// When greater than 1, render at <paramref name="dpi"/> x this factor and area-average
    /// back down. This is not a quality nicety; see the remarks on
    /// <see cref="RenderSupersampled"/>.
    /// </param>
    public byte[] RenderPage(PdfPage page, int pageNumber, double dpi = DefaultDpi, int supersample = 1)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        if (supersample < 1) throw new ArgumentOutOfRangeException(nameof(supersample));

        double scale = dpi / 72.0;

        return supersample > 1
            ? RenderSupersampled(page, pageNumber, scale, supersample)
            : page.RenderTo(pageNumber).WithScale(scale).ToBytes();
    }

    /// <summary>
    /// Renders at <paramref name="factor"/> x the target scale, then filters back down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the fix for a class of page that otherwise OCRs to <em>nothing at all</em>,
    /// silently. Scanned pages commonly embed bilevel CCITT fax images at around 200 DPI.
    /// Asking the rasteriser for any other resolution point-samples them, so each glyph
    /// stroke is shredded into alternating kept and dropped columns; Tesseract's line
    /// recogniser then returns zero bytes with exit code 0 and no warning.
    /// </para>
    /// <para>
    /// Rendering higher does not fix this and rendering lower does not fix it either - both
    /// still point-sample. The fix is to render high and then <em>area-average</em> down, so
    /// each output pixel is the mean of several source pixels and the aliased stripes become
    /// true greyscale edges. On the validation page that motivated this, rendering directly
    /// at 150 DPI recovered 4 words; supersampling recovered 131.
    /// </para>
    /// <para>
    /// The downsample must average, not sample. Skia's default sampling picks nearest or
    /// bilinear neighbours, which at a large reduction still reads only a couple of source
    /// pixels per output pixel and preserves the very aliasing this removes. Mipmapping
    /// builds successively box-filtered levels, so every source pixel contributes - that
    /// averaging is the entire point. Do not "simplify" the sampling options below.
    /// </para>
    /// </remarks>
    private byte[] RenderSupersampled(PdfPage page, int pageNumber, double scale, int factor)
    {
        using SKImage hiImage = page.RenderTo(pageNumber).WithScale(scale * factor).ToImage();
        using SKBitmap hi = SKBitmap.FromImage(hiImage)
            ?? throw new InvalidOperationException(
                $"Supersampled render of page {pageNumber} produced no bitmap.");

        // Greyscale: OCR ignores colour, and averaging one channel avoids paying to filter
        // three identical ones on a scanned page.
        var info = new SKImageInfo(
            Math.Max(1, hi.Width / factor),
            Math.Max(1, hi.Height / factor),
            SKColorType.Gray8,
            SKAlphaType.Opaque);

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        using SKBitmap lo = hi.Resize(info, sampling)
            ?? throw new InvalidOperationException(
                $"Downsample of page {pageNumber} failed ({hi.Width}x{hi.Height} -> {info.Width}x{info.Height}).");

        logger.LogDebug(
            "Supersampled page {PageNumber}: rendered {HiW}x{HiH}, averaged down to {LoW}x{LoH} (factor {Factor})",
            pageNumber, hi.Width, hi.Height, info.Width, info.Height, factor);

        using SKImage img = SKImage.FromBitmap(lo);
        using SKData enc = img.Encode(SKEncodedImageFormat.Png, 100);
        return enc.ToArray();
    }
}
