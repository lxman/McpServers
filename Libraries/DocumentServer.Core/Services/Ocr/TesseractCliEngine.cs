using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DocumentServer.Core.Services.Ocr;

/// <summary>
/// Runs Tesseract as an external process.
/// </summary>
/// <remarks>
/// <para>
/// This deliberately shells out rather than binding Tesseract in-process. Two reasons, both
/// practical rather than stylistic:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Portability.</b> The managed bindings on NuGet ship Windows-only native assets
/// (x64/x86 .dll and nothing else), so an in-process binding cannot run on linux-arm64
/// without symlinking distro libraries to names the binding expects - brittle, and coupled
/// to a specific upstream version.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b><c>OMP_THREAD_LIMIT</c> is a process-scoped setting.</b> Tesseract 5 is built with
/// OpenMP and each process sizes its own thread pool to the whole machine. Running N workers
/// without capping this creates a thread storm: measured at 12 workers on 16 cores, 110
/// pages took 1,343 s with the OpenMP default versus 11 s with the limit set - a 122x
/// difference, with no error and no warning, it simply runs two orders of magnitude slower.
/// Setting it per-process and parallelising across processes is the supported way to get
/// that back.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed class TesseractCliEngine
{
    private readonly ILogger<TesseractCliEngine> _logger;

    /// <summary>Whether a usable Tesseract executable was found.</summary>
    public bool IsAvailable { get; }

    /// <summary>Resolved path to the Tesseract executable, when available.</summary>
    public string? ExecutablePath { get; }

    /// <summary>Version string reported by the executable, when available.</summary>
    public string? Version { get; }

    /// <summary>Language passed to Tesseract via <c>-l</c>.</summary>
    public string Language { get; init; } = "eng";

    /// <summary>Page segmentation mode passed via <c>--psm</c>.</summary>
    public int PageSegmentationMode { get; init; } = 3;

    /// <summary>Per-page timeout. A wedged child process must not stall an ingest run.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    public TesseractCliEngine(ILogger<TesseractCliEngine> logger)
    {
        _logger = logger;

        ExecutablePath = ResolveExecutable();
        if (ExecutablePath is null)
        {
            IsAvailable = false;
            _logger.LogWarning(
                "Tesseract executable not found. Searched TESSERACT_PATH and PATH. OCR is unavailable.");
            return;
        }

        Version = TryReadVersion(ExecutablePath);
        IsAvailable = Version is not null;

        if (IsAvailable)
        {
            _logger.LogInformation(
                "Tesseract OCR available: {Version} at {Path}", Version, ExecutablePath);
        }
        else
        {
            _logger.LogWarning(
                "Tesseract at {Path} did not report a version; treating OCR as unavailable.",
                ExecutablePath);
        }
    }

    /// <summary>
    /// Recognises text in an encoded image (PNG/TIFF/JPEG bytes).
    /// </summary>
    /// <returns>Recognised text, and mean word confidence in 0..1 when requested.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the engine is unavailable or Tesseract fails. This throws rather than
    /// returning empty text on purpose: an OCR failure that reads as "this page has no text"
    /// gets indexed as a legitimately blank page, and the document is then silently
    /// incomplete with nothing to alert on.
    /// </exception>
    public (string Text, float? Confidence) Recognize(byte[] imageBytes, bool withConfidence = false)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (!IsAvailable || ExecutablePath is null)
        {
            throw new InvalidOperationException(
                "OCR is unavailable: no usable Tesseract executable was found. " +
                "Install tesseract-ocr or set TESSERACT_PATH.");
        }

        string workDir = Path.Combine(Path.GetTempPath(), "docserver-ocr-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(workDir);

        try
        {
            string input = Path.Combine(workDir, "page.png");
            string outBase = Path.Combine(workDir, "out");
            File.WriteAllBytes(input, imageBytes);

            // A single invocation can emit several formats, so confidence costs no extra pass.
            string formats = withConfidence ? "txt tsv" : "txt";
            RunTesseract(input, outBase, formats);

            string textPath = outBase + ".txt";
            if (!File.Exists(textPath))
            {
                throw new InvalidOperationException(
                    $"Tesseract reported success but produced no output file at {textPath}.");
            }

            string text = File.ReadAllText(textPath);
            float? confidence = withConfidence ? ReadMeanConfidence(outBase + ".tsv") : null;

            return (text, confidence);
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private void RunTesseract(string input, string outBase, string formats)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExecutablePath!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add(input);
        psi.ArgumentList.Add(outBase);
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add(Language);
        psi.ArgumentList.Add("--psm");
        psi.ArgumentList.Add(PageSegmentationMode.ToString(CultureInfo.InvariantCulture));
        foreach (string f in formats.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            psi.ArgumentList.Add(f);
        }

        // See the class remarks: without this, parallel workers collapse into a thread storm.
        psi.Environment["OMP_THREAD_LIMIT"] = "1";

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // Read both pipes before waiting, or a chatty child can fill a pipe buffer and block.
        Task<string> stdout = proc.StandardOutput.ReadToEndAsync();
        Task<string> stderr = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit((int)Timeout.TotalMilliseconds))
        {
            TryKill(proc);
            throw new InvalidOperationException(
                $"Tesseract timed out after {Timeout.TotalSeconds:F0}s.");
        }

        string err = stderr.GetAwaiter().GetResult();
        _ = stdout.GetAwaiter().GetResult();

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Tesseract exited {proc.ExitCode}: {err.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(err))
        {
            _logger.LogDebug("Tesseract stderr: {Stderr}", err.Trim());
        }
    }

    /// <summary>
    /// Mean per-word confidence from Tesseract's TSV output, normalised to 0..1.
    /// </summary>
    private static float? ReadMeanConfidence(string tsvPath)
    {
        if (!File.Exists(tsvPath)) return null;

        var total = 0.0;
        var count = 0;

        foreach (string line in File.ReadLines(tsvPath).Skip(1))
        {
            string[] cols = line.Split('\t');
            if (cols.Length < 12) continue;

            // Column 11 is conf; -1 marks structural rows (block/para/line) rather than words.
            if (!float.TryParse(cols[10], NumberStyles.Float, CultureInfo.InvariantCulture, out float conf))
                continue;
            if (conf < 0) continue;
            if (string.IsNullOrWhiteSpace(cols[11])) continue;

            total += conf;
            count++;
        }

        return count == 0 ? null : (float)(total / count / 100.0);
    }

    private static string? ResolveExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("TESSERACT_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string exeName = OperatingSystem.IsWindows() ? "tesseract.exe" : "tesseract";

        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar is not null)
        {
            foreach (string dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry; skip it.
                }
            }
        }

        return null;
    }

    private string? TryReadVersion(string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(15_000))
            {
                TryKill(proc);
                return null;
            }

            string? first = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return first?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read Tesseract version from {Path}", exePath);
            return null;
        }
    }

    private static void TryKill(Process proc)
    {
        try { proc.Kill(entireProcessTree: true); }
        catch (Exception) { /* already gone */ }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (Exception) { /* best effort temp cleanup */ }
    }
}
