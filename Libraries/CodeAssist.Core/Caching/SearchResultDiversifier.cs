using CodeAssist.Core.Models;

namespace CodeAssist.Core.Caching;

/// <summary>
/// Reduces redundant enclosing and overlapping chunks while retaining the strongest matches.
/// </summary>
public static class SearchResultDiversifier
{
    public static List<ExactSymbolMatch> ResolveFreshExactMatches(
        IEnumerable<SearchResult> indexedMatches,
        string requestedSymbol,
        Func<string, CachedFile?> cachedFileLookup)
    {
        var resolved = new List<ExactSymbolMatch>();

        foreach (IGrouping<string, SearchResult> fileGroup in indexedMatches.GroupBy(
                     result => result.Chunk.FilePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            CachedFile? cachedFile = string.IsNullOrWhiteSpace(fileGroup.Key)
                ? null
                : cachedFileLookup(fileGroup.Key);

            if (cachedFile == null)
            {
                resolved.AddRange(fileGroup.Select(result => new ExactSymbolMatch(result.Chunk, false)));
                continue;
            }

            // The hot cache is authoritative for a changed file. Re-resolve the requested symbol
            // against its current chunks instead of returning an exact but stale Qdrant payload.
            resolved.AddRange(cachedFile.Chunks
                .Where(chunk => chunk.SymbolName is { Length: > 0 } symbolName
                    && RemovePartSuffix(symbolName).Equals(
                        requestedSymbol,
                        StringComparison.OrdinalIgnoreCase))
                .Select(chunk => new ExactSymbolMatch(chunk, true)));
        }

        return resolved;
    }

    public static List<UnifiedSearchHit> Diversify(
        IEnumerable<UnifiedSearchHit> candidates,
        int limit,
        int maxResultsPerFile = 2)
    {
        limit = Math.Clamp(limit, 1, 100);
        maxResultsPerFile = Math.Clamp(maxResultsPerFile, 1, 10);

        List<UnifiedSearchHit> ranked = candidates
            .OrderByDescending(hit => hit.Score + SpecificityBoost(hit.Chunk.ChunkType))
            .ToList();
        var selected = new List<UnifiedSearchHit>(Math.Min(limit, ranked.Count));
        var fileCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (UnifiedSearchHit candidate in ranked)
        {
            string path = candidate.Chunk.RelativePath;
            if (fileCounts.GetValueOrDefault(path) >= maxResultsPerFile)
                continue;

            bool redundant = selected.Any(existing =>
                existing.Chunk.RelativePath.Equals(path, StringComparison.OrdinalIgnoreCase)
                && HasSubstantialOverlap(existing.Chunk, candidate.Chunk));
            if (redundant)
                continue;

            selected.Add(candidate);
            fileCounts[path] = fileCounts.GetValueOrDefault(path) + 1;
            if (selected.Count == limit)
                break;
        }

        return selected;
    }

    public static string BaseChunkType(string chunkType)
    {
        int partIndex = chunkType.LastIndexOf("_part", StringComparison.Ordinal);
        if (partIndex < 0) return chunkType;

        ReadOnlySpan<char> suffix = chunkType.AsSpan(partIndex + 5);
        return !suffix.IsEmpty && suffix.IndexOfAnyExceptInRange('0', '9') < 0
            ? chunkType[..partIndex]
            : chunkType;
    }

    public static string RemovePartSuffix(string value)
    {
        const string marker = " (part ";
        int markerIndex = value.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 || value[^1] != ')') return value;

        ReadOnlySpan<char> partNumber = value.AsSpan(markerIndex + marker.Length, value.Length - markerIndex - marker.Length - 1);
        return !partNumber.IsEmpty && partNumber.IndexOfAnyExceptInRange('0', '9') < 0
            ? value[..markerIndex]
            : value;
    }

    private static float SpecificityBoost(string chunkType)
    {
        return BaseChunkType(chunkType) switch
        {
            "method" or "function" or "constructor" or "property" => 0.025f,
            "class" or "record" or "struct" or "interface" => 0.01f,
            "file_segment" or "file" => -0.01f,
            _ => 0f
        };
    }

    private static bool HasSubstantialOverlap(CodeChunk left, CodeChunk right)
    {
        int intersection = Math.Min(left.EndLine, right.EndLine) - Math.Max(left.StartLine, right.StartLine) + 1;
        if (intersection <= 0) return false;

        int shorterLength = Math.Min(
            left.EndLine - left.StartLine + 1,
            right.EndLine - right.StartLine + 1);
        return intersection >= shorterLength * 0.75;
    }
}

public sealed record ExactSymbolMatch(CodeChunk Chunk, bool IsFresh);
