using System.Security.Cryptography;
using System.Text;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Chunking;

/// <summary>
/// Default chunker that splits code by line count with overlap.
/// Used for languages without specialized parsing support.
/// </summary>
public sealed class DefaultChunker : ICodeChunker
{
    private readonly CodeAssistOptions _options;
    private readonly ILogger<DefaultChunker> _logger;

    // Empty set - this is the fallback chunker
    private static readonly HashSet<string> SupportedLangs = [];

    public DefaultChunker(IOptions<CodeAssistOptions> options, ILogger<DefaultChunker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlySet<string> SupportedLanguages => SupportedLangs;

    /// <summary>
    /// Default chunker supports any language as a fallback.
    /// </summary>
    public bool SupportsLanguage(string language) => true;

    public IReadOnlyList<CodeChunk> ChunkCode(string content, string filePath, string relativePath, string language)
    {
        var chunks = new List<CodeChunk>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return chunks;
        }

        var lines = content.Split('\n');
        var totalLines = lines.Length;

        // Calculate lines per chunk based on max chunk size
        // Assume average line length of ~60 chars
        var linesPerChunk = Math.Max(10, _options.MaxChunkSize / 60);
        var overlapLines = Math.Max(2, _options.ChunkOverlap / 60);

        _logger.LogDebug("Chunking {File}: {Lines} lines, linesPerChunk={LPC}, overlap={OL}",
            relativePath, totalLines, linesPerChunk, overlapLines);

        if (totalLines <= linesPerChunk)
        {
            // Small file - single chunk
            _logger.LogDebug("Small file, creating single chunk");
            chunks.Add(CreateChunk(content, filePath, relativePath, 1, totalLines, language));
            return chunks;
        }

        // Split into overlapping chunks
        var startLine = 0;
        var iteration = 0;
        while (startLine < totalLines)
        {
            iteration++;
            if (iteration > 1000)
            {
                _logger.LogError("Infinite loop detected in chunking! startLine={Start}, totalLines={Total}", startLine, totalLines);
                break;
            }

            var endLine = Math.Min(startLine + linesPerChunk, totalLines);
            var chunkLines = lines[startLine..endLine];
            var chunkContent = string.Join('\n', chunkLines);

            chunks.Add(CreateChunk(
                chunkContent,
                filePath,
                relativePath,
                startLine + 1,  // 1-indexed
                endLine,        // 1-indexed
                language));

            // Move to next chunk with overlap, ensuring we always advance
            var nextStartLine = endLine - overlapLines;
            if (nextStartLine <= startLine)
            {
                nextStartLine = startLine + 1; // Ensure progress
            }
            startLine = nextStartLine;
        }

        _logger.LogDebug("Created {Count} chunks for {File}", chunks.Count, relativePath);
        return chunks;
    }

    private CodeChunk CreateChunk(
        string content,
        string filePath,
        string relativePath,
        int startLine,
        int endLine,
        string language)
    {
        return new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = filePath,
            RelativePath = relativePath,
            Content = content,
            StartLine = startLine,
            EndLine = endLine,
            ChunkType = "file_segment",
            SymbolName = null,
            ParentSymbol = null,
            Language = language.ToLowerInvariant(),
            ContentHash = ComputeHash(content)
        };
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
