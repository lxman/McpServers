using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// Runs the render-and-OCR path against a real corpus, on the machine it will ship to.
/// </summary>
/// <remarks>
/// <para>
/// Three modes. <c>pages</c> re-runs a manifest of individual pages, which is how the ARM64
/// render parity check was done: SkiaSharp's linux-arm64 asset and PdfLibrary's rasteriser have
/// no ARM64 guarantee, and a rasteriser that quietly renders differently on another
/// architecture does not throw - it produces worse text, invisible without a reference.
/// </para>
/// <para>
/// <c>doc</c> runs the full <see cref="OcrService"/> over one PDF, exercising page gating and
/// error accounting. <c>fulltext</c> is the production job: it fills in the OCR-pending pages of
/// an extracted corpus in place.
/// </para>
/// </remarks>
public static class Program
{
    public static int Main(string[] args)
    {
        string mode = Arg(args, "--mode") ?? "pages";
        return mode switch
        {
            "pages" => RunPages(args),
            "doc" => RunDoc(args),
            "fulltext" => RunFulltext(args),
            _ => Fail($"unknown --mode '{mode}' (expected 'pages', 'doc' or 'fulltext')")
        };
    }

    // ---------------------------------------------------------------- pages

    private static int RunPages(string[] args)
    {
        string manifest = Arg(args, "--manifest") ?? Fail<string>("--manifest is required");
        string corpus = Arg(args, "--corpus") ?? Fail<string>("--corpus is required");
        string outDir = Arg(args, "--out") ?? Fail<string>("--out is required");
        int limit = int.TryParse(Arg(args, "--limit"), out int l) ? l : int.MaxValue;

        Deps deps = BuildDeps(NullLoggerFactory.Instance);
        if (!deps.Engine.IsAvailable) return Fail("Tesseract is not available on this machine.");

        PrintEnvironment(deps.Engine);

        List<PageRef> pages = (JsonSerializer.Deserialize<List<PageRef>>(
            File.ReadAllText(manifest), JsonOpts) ?? []).Take(limit).ToList();

        string textDir = Path.Combine(outDir, "text");
        Directory.CreateDirectory(textDir);

        var results = new List<PageResult>();
        var sw = Stopwatch.StartNew();

        // Group by source so each PDF is parsed once rather than once per page; several of
        // these documents run past a hundred pages.
        foreach (IGrouping<string, PageRef> group in pages.GroupBy(p => p.Source))
        {
            PdfDocument doc;
            try
            {
                doc = PdfDocument.Load(Path.Combine(corpus, Normalize(group.Key)));
            }
            catch (Exception ex)
            {
                foreach (PageRef p in group)
                {
                    results.Add(new PageResult { Id = p.Id, Error = $"load failed: {ex.Message}" });
                    Console.WriteLine($"  FAIL {p.Id}: load failed: {ex.Message}");
                }

                continue;
            }

            try
            {
                foreach (PageRef p in group)
                {
                    results.Add(ProcessManifestPage(doc, p, deps, textDir));
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
            tesseract = deps.Engine.Version,
            pages = results.Count,
            failed = results.Count(r => r.Error is not null),
            retried = results.Count(r => r.Retried),
            rescued = results.Count(r => r.Rescued),
            emptyPages = results.Count(r => r.Error is null && r.Words == 0),
            totalWords = results.Sum(r => r.Words),
            elapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1),
            results
        };

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "_summary.json"),
            JsonSerializer.Serialize(summary, Indented));

        Console.WriteLine($"\npages {summary.pages} | failed {summary.failed} | "
            + $"empty {summary.emptyPages} | retried {summary.retried} "
            + $"(rescued {summary.rescued}) | words {summary.totalWords} | "
            + $"{summary.elapsedSeconds}s");

        return summary.failed > 0 ? 1 : 0;
    }

    private static PageResult ProcessManifestPage(
        PdfDocument doc, PageRef p, Deps deps, string textDir)
    {
        try
        {
            PdfPage page = doc.GetPage(p.Page - 1)
                ?? throw new InvalidOperationException($"page {p.Page} did not load");

            // A fresh strategy per page: the manifest samples non-consecutive pages, so
            // carrying latch state between them would model a document that is not there.
            var strategy = new PageOcrStrategy(
                NullLogger<PageOcrStrategy>.Instance, deps.Engine, deps.Rasterizer);
            PageOcrOutcome o = strategy.OcrPage(page, p.Page);

            File.WriteAllText(Path.Combine(textDir, p.Id + ".txt"), o.Text);
            Console.WriteLine($"  {p.Id,-40} {o.Words,6} words"
                + (o.Rescued ? "  (rescued)" : o.Retried ? "  (retried)" : ""));

            return new PageResult
            {
                Id = p.Id, Words = o.Words, Chars = o.Text.Length,
                Retried = o.Retried, Rescued = o.Rescued
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL {p.Id}: {ex.Message}");
            return new PageResult { Id = p.Id, Error = ex.Message };
        }
    }

    // ------------------------------------------------------------- fulltext

    /// <summary>
    /// Fills in the OCR-pending pages of an extracted corpus, in place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each <c>fulltext/*.json</c> holds a document's pages, with <c>needsOcr</c> marking those
    /// that had no text layer. This renders and recognises exactly those pages and writes the
    /// text back, so the downstream structure parser needs no separate OCR path.
    /// </para>
    /// <para>
    /// A page that OCR cannot read <b>keeps</b> <c>needsOcr: true</c>. Clearing the flag with
    /// empty text would turn an unreadable page into a legitimately blank one, and the document
    /// would then be silently incomplete with nothing to alert on. Leaving it set keeps it an
    /// honest gap that the parser already knows how to handle.
    /// </para>
    /// </remarks>
    private static int RunFulltext(string[] args)
    {
        string dir = Arg(args, "--fulltext") ?? Fail<string>("--fulltext is required");
        string corpus = Arg(args, "--corpus") ?? Fail<string>("--corpus is required");
        string reportPath = Arg(args, "--report") ?? "ocr_fill_report.json";
        int parallel = int.TryParse(Arg(args, "--parallel"), out int p) ? p : 1;

        Deps deps = BuildDeps(NullLoggerFactory.Instance);
        if (!deps.Engine.IsAvailable) return Fail("Tesseract is not available on this machine.");

        PrintEnvironment(deps.Engine);
        Console.WriteLine($"workers       : {parallel}");

        string[] files = Directory.GetFiles(dir, "*.json").OrderBy(f => f).ToArray();
        var pending = new List<(string File, JsonNode Root, List<int> Pages)>();

        foreach (string f in files)
        {
            JsonNode root = JsonNode.Parse(File.ReadAllText(f))
                ?? throw new InvalidOperationException($"{f} is not valid JSON");

            JsonArray pagesArr = root["pages"]!.AsArray();
            var need = new List<int>();
            for (var i = 0; i < pagesArr.Count; i++)
            {
                if (pagesArr[i]!["needsOcr"]!.GetValue<bool>()) need.Add(i);
            }

            if (need.Count > 0) pending.Add((f, root, need));
        }

        Console.WriteLine($"documents     : {pending.Count} of {files.Length} need OCR");
        Console.WriteLine($"pages         : {pending.Sum(x => x.Pages.Count)}\n");

        var docResults = new ConcurrentBag<DocResult>();
        var done = 0;
        var sw = Stopwatch.StartNew();

        Parallel.ForEach(pending, new ParallelOptions { MaxDegreeOfParallelism = parallel }, item =>
        {
            DocResult r = FillDocument(item.File, item.Root, item.Pages, corpus, deps);
            docResults.Add(r);

            int n = Interlocked.Increment(ref done);
            Console.WriteLine($"  [{n}/{pending.Count}] {r.DocId,-32} "
                + $"{r.PagesOcrd}/{r.PagesRequested} pages, {r.Words} words"
                + (r.Unreadable > 0 ? $", {r.Unreadable} unreadable" : "")
                + (r.Error is not null ? $"  ERROR {r.Error}" : ""));
        });

        sw.Stop();

        List<DocResult> all = docResults.OrderBy(r => r.DocId).ToList();
        var report = new
        {
            architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            tesseract = deps.Engine.Version,
            workers = parallel,
            documents = all.Count,
            documentsFailed = all.Count(r => r.Error is not null),
            pagesRequested = all.Sum(r => r.PagesRequested),
            pagesOcrd = all.Sum(r => r.PagesOcrd),
            pagesUnreadable = all.Sum(r => r.Unreadable),
            pagesRescued = all.Sum(r => r.Rescued),
            totalWords = all.Sum(r => r.Words),
            elapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1),
            documentsDetail = all
        };

        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, Indented));

        Console.WriteLine($"\ndocuments     : {report.documents} ({report.documentsFailed} failed)");
        Console.WriteLine($"pages OCR'd   : {report.pagesOcrd} / {report.pagesRequested}");
        Console.WriteLine($"unreadable    : {report.pagesUnreadable}  (left needsOcr=true)");
        Console.WriteLine($"rescued       : {report.pagesRescued}");
        Console.WriteLine($"words         : {report.totalWords}");
        Console.WriteLine($"elapsed       : {report.elapsedSeconds}s "
            + $"({report.pagesRequested / Math.Max(1.0, report.elapsedSeconds):F1} pages/s)");
        Console.WriteLine($"report        : {reportPath}");

        return report.documentsFailed > 0 ? 1 : 0;
    }

    private static DocResult FillDocument(
        string file, JsonNode root, List<int> pageIndexes, string corpus, Deps deps)
    {
        var result = new DocResult
        {
            DocId = root["docId"]!.GetValue<string>(),
            Source = root["sourcePath"]!.GetValue<string>(),
            PagesRequested = pageIndexes.Count
        };

        PdfDocument doc;
        try
        {
            doc = PdfDocument.Load(Path.Combine(corpus, Normalize(result.Source)));
        }
        catch (Exception ex)
        {
            // Encrypted or corrupt: surface it. Never write empty text and clear the flag.
            result.Error = $"load failed: {ex.Message}";
            return result;
        }

        try
        {
            JsonArray pagesArr = root["pages"]!.AsArray();
            var strategy = new PageOcrStrategy(
                NullLogger<PageOcrStrategy>.Instance, deps.Engine, deps.Rasterizer);

            foreach (int idx in pageIndexes)
            {
                JsonNode pageNode = pagesArr[idx]!;
                int pageNumber = pageNode["page"]!.GetValue<int>();

                try
                {
                    PdfPage page = doc.GetPage(pageNumber - 1)
                        ?? throw new InvalidOperationException("page did not load");

                    PageOcrOutcome o = strategy.OcrPage(page, pageNumber);

                    if (o.Words == 0)
                    {
                        result.Unreadable++;
                        result.UnreadablePages.Add(pageNumber);
                        continue;
                    }

                    pageNode["text"] = o.Text;
                    pageNode["needsOcr"] = false;
                    // Provenance: downstream may want to weight or re-check OCR'd text, and
                    // after the fact there is otherwise no way to tell it apart.
                    pageNode["ocr"] = true;

                    result.PagesOcrd++;
                    result.Words += o.Words;
                    if (o.Rescued) result.Rescued++;
                }
                catch (Exception ex)
                {
                    result.Unreadable++;
                    result.UnreadablePages.Add(pageNumber);
                    result.PageErrors.Add($"page {pageNumber}: {ex.Message}");
                }
            }
        }
        finally
        {
            doc.Dispose();
        }

        if (result.PagesOcrd > 0)
        {
            File.WriteAllText(file, root.ToJsonString(Indented));
        }

        return result;
    }

    // ------------------------------------------------------------------ doc

    private static int RunDoc(string[] args)
    {
        string pdf = Arg(args, "--pdf") ?? Fail<string>("--pdf is required");
        string? outPath = Arg(args, "--out");

        ILoggerFactory loggerFactory = LoggerFactory.Create(b => b
            .SetMinimumLevel(LogLevel.Information)
            .AddSimpleConsole(o => o.SingleLine = true));

        Deps deps = BuildDeps(loggerFactory);
        var preprocessor = new ImagePreprocessor(loggerFactory.CreateLogger<ImagePreprocessor>());

        using var service = new OcrService(
            loggerFactory.CreateLogger<OcrService>(),
            loggerFactory.CreateLogger<PageOcrStrategy>(),
            deps.Engine, deps.Rasterizer, preprocessor);

        if (!service.IsAvailable) return Fail("Tesseract is not available on this machine.");

        var sw = Stopwatch.StartNew();
        OcrResult result = service.ExtractTextFromScannedPdf(pdf).GetAwaiter().GetResult();
        sw.Stop();

        Console.WriteLine($"\nsuccess         : {result.Success}");
        Console.WriteLine($"error           : {result.ErrorMessage ?? "-"}");
        Console.WriteLine($"pages OCR'd     : {result.PagesProcessed}");
        Console.WriteLine($"pages w/ errors : {result.PagesWithErrors}");
        Console.WriteLine($"elapsed         : {sw.Elapsed.TotalSeconds:F1}s");
        foreach (KeyValuePair<string, string> kv in result.Metadata.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {kv.Key,-26}: {kv.Value}");
        }

        if (outPath is not null) File.WriteAllText(outPath, result.ExtractedText ?? string.Empty);

        return result.Success ? 0 : 1;
    }

    // --------------------------------------------------------------- shared

    private sealed record Deps(TesseractCliEngine Engine, PdfPageRasterizer Rasterizer);

    private static Deps BuildDeps(ILoggerFactory f) => new(
        new TesseractCliEngine(f.CreateLogger<TesseractCliEngine>()),
        new PdfPageRasterizer(f.CreateLogger<PdfPageRasterizer>()));

    private static void PrintEnvironment(TesseractCliEngine engine)
    {
        Console.WriteLine($"tesseract     : {engine.Version}");
        Console.WriteLine($"architecture  : {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"runtime id    : {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
    }

    /// <summary>Corpus paths are recorded Windows-style; make them work on either host.</summary>
    private static string Normalize(string relative)
        => relative.Replace('\\', Path.DirectorySeparatorChar);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        // Contract text is full of characters that would otherwise be escaped into unreadable
        // \uXXXX noise, and these files get read by hand during triage.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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
    }

    private sealed class PageResult
    {
        public string Id { get; set; } = "";
        public int Words { get; set; }
        public int Chars { get; set; }
        public bool Retried { get; set; }
        public bool Rescued { get; set; }
        public string? Error { get; set; }
    }

    private sealed class DocResult
    {
        public string DocId { get; set; } = "";
        public string Source { get; set; } = "";
        public int PagesRequested { get; set; }
        public int PagesOcrd { get; set; }
        public int Unreadable { get; set; }
        public int Rescued { get; set; }
        public int Words { get; set; }
        public string? Error { get; set; }
        public List<int> UnreadablePages { get; set; } = [];
        public List<string> PageErrors { get; set; } = [];
    }
}
