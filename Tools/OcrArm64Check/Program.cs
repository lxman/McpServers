using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentServer.Core.Services.Ocr;
using DocumentServer.Core.Services.Ocr.Models;
using DocumentServer.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PdfLibrary.Document;
using PdfLibrary.Structure;

namespace OcrArm64Check;

/// <summary>
/// Verifies the render-and-OCR path on linux-arm64 against a reference run.
/// </summary>
/// <remarks>
/// <para>
/// The OCR engine choice was already settled and Tesseract itself was confirmed on ARM64. What
/// this checks is the <em>managed</em> path either side of it: SkiaSharp's linux-arm64 native
/// asset and PdfLibrary's rasteriser. Those are the pieces with no ARM64 guarantee, and a
/// rasteriser that quietly renders differently on another architecture does not throw - it just
/// produces worse text, which is invisible without a reference to compare against.
/// </para>
/// <para>
/// So this deliberately re-runs the same pages the bake-off scored and diffs the text, rather
/// than asserting that the process merely starts.
/// </para>
/// </remarks>
public static class Program
{
    /// <summary>Word count at or below which the supersampled retry fires. Matches OcrService.</summary>
    private const int RetryWordThreshold = 5;

    public static int Main(string[] args)
    {
        string mode = Arg(args, "--mode") ?? "pages";
        return mode switch
        {
            "pages" => RunPages(args),
            "doc" => RunDoc(args),
            _ => Fail($"unknown --mode '{mode}' (expected 'pages' or 'doc')")
        };
    }

    private static int RunPages(string[] args)
    {
        string manifest = Arg(args, "--manifest") ?? Fail<string>("--manifest is required");
        string corpus = Arg(args, "--corpus") ?? Fail<string>("--corpus is required");
        string outDir = Arg(args, "--out") ?? Fail<string>("--out is required");
        int limit = int.TryParse(Arg(args, "--limit"), out int l) ? l : int.MaxValue;
        // Saving rasters is what makes this a test of the *renderer*. Without them a text
        // difference cannot be attributed to the render or the engine.
        string? renderDir = Arg(args, "--renders");

        ILoggerFactory loggerFactory = NullLoggerFactory.Instance;
        var engine = new TesseractCliEngine(loggerFactory.CreateLogger<TesseractCliEngine>());
        if (!engine.IsAvailable)
        {
            return Fail("Tesseract is not available on this machine.");
        }

        Console.WriteLine($"tesseract     : {engine.Version}");
        Console.WriteLine($"architecture  : {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"runtime id    : {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");

        var rasterizer = new PdfPageRasterizer(loggerFactory.CreateLogger<PdfPageRasterizer>());

        List<PageRef> pages = JsonSerializer.Deserialize<List<PageRef>>(
            File.ReadAllText(manifest), JsonOpts) ?? [];
        pages = pages.Take(limit).ToList();

        Directory.CreateDirectory(outDir);
        string textDir = Path.Combine(outDir, "text");
        Directory.CreateDirectory(textDir);
        if (renderDir is not null) Directory.CreateDirectory(renderDir);

        var results = new List<PageResult>();
        var sw = Stopwatch.StartNew();

        // Group by source so each PDF is parsed once rather than once per page; several of
        // these documents are over a hundred pages.
        foreach (IGrouping<string, PageRef> group in pages.GroupBy(p => p.Source))
        {
            string pdfPath = Path.Combine(corpus, group.Key.Replace('\\', Path.DirectorySeparatorChar));
            PdfDocument? doc = null;

            try
            {
                doc = PdfDocument.Load(pdfPath);
            }
            catch (Exception ex)
            {
                foreach (PageRef p in group)
                {
                    results.Add(PageResult.Failed(p, $"load failed: {ex.Message}"));
                    Console.WriteLine($"  FAIL {p.Id}: load failed: {ex.Message}");
                }
                continue;
            }

            try
            {
                foreach (PageRef p in group)
                {
                    results.Add(ProcessPage(doc, p, rasterizer, engine, textDir, renderDir));
                }
            }
            finally
            {
                doc.Dispose();
            }
        }

        sw.Stop();

        var summary = new
        {
            architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            runtimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            tesseract = engine.Version,
            pages = results.Count,
            failed = results.Count(r => r.Error is not null),
            retried = results.Count(r => r.Retried),
            rescued = results.Count(r => r.Rescued),
            emptyPages = results.Count(r => r.Error is null && r.Words == 0),
            totalWords = results.Sum(r => r.Words),
            elapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1),
            results
        };

        File.WriteAllText(Path.Combine(outDir, "_summary.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"pages        : {summary.pages}");
        Console.WriteLine($"failed       : {summary.failed}");
        Console.WriteLine($"empty        : {summary.emptyPages}");
        Console.WriteLine($"retried      : {summary.retried}  (rescued {summary.rescued})");
        Console.WriteLine($"total words  : {summary.totalWords}");
        Console.WriteLine($"elapsed      : {summary.elapsedSeconds}s");
        Console.WriteLine($"wrote        : {outDir}");

        return summary.failed > 0 ? 1 : 0;
    }

    /// <summary>
    /// Renders and recognises one page, mirroring OcrService's two-pass strategy.
    /// </summary>
    private static PageResult ProcessPage(
        PdfDocument doc, PageRef p, PdfPageRasterizer rasterizer,
        TesseractCliEngine engine, string textDir, string? renderDir)
    {
        try
        {
            PdfPage page = doc.GetPage(p.Page - 1)
                ?? throw new InvalidOperationException($"page {p.Page} did not load");

            byte[] raster = rasterizer.RenderPage(page, p.Page, PdfPageRasterizer.DefaultDpi);
            (string text, _) = engine.Recognize(raster);
            int words = CountWords(text);

            // Always keep the first-pass raster under the plain id: that is the one the x64
            // reference renders correspond to, so the comparison stays like-for-like even on
            // pages where the retry later wins.
            if (renderDir is not null)
            {
                File.WriteAllBytes(Path.Combine(renderDir, p.Id + ".png"), raster);
            }

            string firstPassSha = Convert.ToHexString(SHA256.HashData(raster))[..16];
            var retried = false;
            var rescued = false;

            if (words <= RetryWordThreshold)
            {
                retried = true;
                byte[] retryRaster = rasterizer.RenderPage(
                    page, p.Page, PdfPageRasterizer.RetryDpi, PdfPageRasterizer.RetrySupersample);
                (string retryText, _) = engine.Recognize(retryRaster);
                int retryWords = CountWords(retryText);

                if (renderDir is not null)
                {
                    File.WriteAllBytes(Path.Combine(renderDir, p.Id + ".retry.png"), retryRaster);
                }

                if (retryWords > words)
                {
                    rescued = true;
                    text = retryText;
                    words = retryWords;
                    raster = retryRaster;
                }
            }

            File.WriteAllText(Path.Combine(textDir, p.Id + ".txt"), text);

            Console.WriteLine(
                $"  {p.Id,-40} {words,6} words{(rescued ? "  (rescued)" : retried ? "  (retried)" : "")}");

            return new PageResult
            {
                Id = p.Id,
                Words = words,
                Chars = text.Length,
                Retried = retried,
                Rescued = rescued,
                FirstPassSha256 = firstPassSha,
                RasterSha256 = Convert.ToHexString(SHA256.HashData(raster))[..16],
                RasterBytes = raster.Length
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL {p.Id}: {ex.Message}");
            return PageResult.Failed(p, ex.Message);
        }
    }

    /// <summary>
    /// Runs the real <see cref="OcrService"/> over a whole PDF, which the per-page mode does
    /// not cover: page gating, the supersample latch, and error accounting only exist there.
    /// </summary>
    private static int RunDoc(string[] args)
    {
        string pdf = Arg(args, "--pdf") ?? Fail<string>("--pdf is required");
        string? outPath = Arg(args, "--out");

        ILoggerFactory loggerFactory = LoggerFactory.Create(b => b
            .SetMinimumLevel(LogLevel.Information)
            .AddSimpleConsole(o => o.SingleLine = true));

        var engine = new TesseractCliEngine(loggerFactory.CreateLogger<TesseractCliEngine>());
        var rasterizer = new PdfPageRasterizer(loggerFactory.CreateLogger<PdfPageRasterizer>());
        var preprocessor = new ImagePreprocessor(loggerFactory.CreateLogger<ImagePreprocessor>());

        using var service = new OcrService(
            loggerFactory.CreateLogger<OcrService>(), engine, rasterizer, preprocessor);

        if (!service.IsAvailable)
        {
            return Fail("Tesseract is not available on this machine.");
        }

        var sw = Stopwatch.StartNew();
        OcrResult result = service.ExtractTextFromScannedPdf(pdf).GetAwaiter().GetResult();
        sw.Stop();

        Console.WriteLine();
        Console.WriteLine($"success         : {result.Success}");
        Console.WriteLine($"error           : {result.ErrorMessage ?? "-"}");
        Console.WriteLine($"pages OCR'd     : {result.PagesProcessed}");
        Console.WriteLine($"pages w/ errors : {result.PagesWithErrors}");
        Console.WriteLine($"words           : {CountWords(result.ExtractedText ?? string.Empty)}");
        Console.WriteLine($"elapsed         : {sw.Elapsed.TotalSeconds:F1}s");
        foreach (KeyValuePair<string, string> kv in result.Metadata.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {kv.Key,-26}: {kv.Value}");
        }

        if (outPath is not null)
        {
            File.WriteAllText(outPath, result.ExtractedText ?? string.Empty);
            Console.WriteLine($"wrote           : {outPath}");
        }

        return result.Success ? 0 : 1;
    }

    private static int CountWords(string text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("ERROR: " + message);
        return 2;
    }

    private static T Fail<T>(string message)
    {
        Console.Error.WriteLine("ERROR: " + message);
        Environment.Exit(2);
        return default!;
    }

    private sealed class PageRef
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("source")] public string Source { get; set; } = "";
        [JsonPropertyName("page")] public int Page { get; set; }
        [JsonPropertyName("dpi")] public int Dpi { get; set; }
    }

    private sealed class PageResult
    {
        public string Id { get; set; } = "";
        public int Words { get; set; }
        public int Chars { get; set; }
        public bool Retried { get; set; }
        public bool Rescued { get; set; }
        public string? FirstPassSha256 { get; set; }
        public string? RasterSha256 { get; set; }
        public int RasterBytes { get; set; }
        public string? Error { get; set; }

        public static PageResult Failed(PageRef p, string error)
            => new() { Id = p.Id, Error = error };
    }
}
