using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Caching;

/// <summary>
/// Unified search service that combines L1 (hot cache) and L2 (Qdrant) results.
/// L1 results are always fresh (from recently changed files).
/// L2 results are used for discovery across the full codebase.
/// When files overlap, L1 content takes priority.
/// </summary>
public sealed class UnifiedSearchService(
    HotCache hotCache,
    QdrantService qdrantService,
    OllamaService embeddingService,
    IOptions<CodeAssistOptions> options,
    ILogger<UnifiedSearchService> logger)
{
    private readonly CodeAssistOptions _options = options.Value;

    /// <summary>
    /// Search across both L1 (hot cache) and L2 (Qdrant).
    /// Results from hot files are always fresh.
    /// </summary>
    public async Task<UnifiedSearchResult> SearchAsync(
        string query,
        string collectionName,
        int limit = 10,
        float minScore = 0.5f,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Generate query embedding once
        var queryEmbedding = await embeddingService.GetEmbeddingAsync(query, cancellationToken);

        // Search L1 (hot cache) - always fresh
        var l1Task = SearchL1Async(queryEmbedding, limit, minScore, cancellationToken);

        // Search L2 (Qdrant) - full codebase
        var l2Task = SearchL2Async(collectionName, queryEmbedding, limit, minScore, cancellationToken);

        await Task.WhenAll(l1Task, l2Task);

        var l1Results = await l1Task;
        var l2Results = await l2Task;

        // Merge results with L1 priority
        var mergedResults = MergeResults(l1Results, l2Results, limit);

        stopwatch.Stop();

        var result = new UnifiedSearchResult
        {
            Query = query,
            Results = mergedResults,
            L1HitCount = l1Results.Count,
            L2HitCount = l2Results.Count,
            TotalResultCount = mergedResults.Count,
            Duration = stopwatch.Elapsed,
            HotFilesSearched = hotCache.Count
        };

        logger.LogDebug(
            "Unified search completed in {Duration}ms: L1={L1Count}, L2={L2Count}, Merged={MergedCount}",
            stopwatch.ElapsedMilliseconds, l1Results.Count, l2Results.Count, mergedResults.Count);

        return result;
    }

    private async Task<List<UnifiedSearchHit>> SearchL1Async(
        float[] queryEmbedding,
        int limit,
        float minScore,
        CancellationToken cancellationToken)
    {
        try
        {
            var l1Results = await hotCache.SearchAsync(
                "", // Query embedding already computed, pass empty string
                limit,
                minScore,
                cancellationToken);

            // Re-score using the provided embedding
            var results = new List<UnifiedSearchHit>();

            foreach (var r in l1Results)
            {
                var score = CosineSimilarity(queryEmbedding, r.Embedding);
                if (score >= minScore)
                {
                    results.Add(new UnifiedSearchHit
                    {
                        Chunk = r.Chunk,
                        Score = score,
                        Source = SearchSource.L1HotCache,
                        IsFresh = true,
                        CachedAt = r.CachedFile.CachedAt
                    });
                }
            }

            return results.OrderByDescending(r => r.Score).Take(limit).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "L1 search failed, continuing with L2 only");
            return [];
        }
    }

    private async Task<List<UnifiedSearchHit>> SearchL2Async(
        string collectionName,
        float[] queryEmbedding,
        int limit,
        float minScore,
        CancellationToken cancellationToken)
    {
        try
        {
            var l2Results = await qdrantService.SearchAsync(
                collectionName,
                queryEmbedding,
                limit,
                minScore,
                cancellationToken: cancellationToken);

            return l2Results.Select(r => new UnifiedSearchHit
            {
                Chunk = r.Chunk,
                Score = r.Score,
                Source = SearchSource.L2Qdrant,
                IsFresh = false, // Will be updated if file is in hot cache
                CachedAt = null
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "L2 search failed for collection {Collection}", collectionName);
            return [];
        }
    }

    private List<UnifiedSearchHit> MergeResults(
        List<UnifiedSearchHit> l1Results,
        List<UnifiedSearchHit> l2Results,
        int limit)
    {
        // Build set of hot file paths for quick lookup
        var hotFilePaths = new HashSet<string>(
            l1Results.Select(r => r.Chunk.FilePath),
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<UnifiedSearchHit>();

        // Add all L1 results (always fresh)
        merged.AddRange(l1Results);

        // Add L2 results, but replace content for hot files
        foreach (var l2Hit in l2Results)
        {
            // Skip if we already have this file from L1 (L1 has fresher content)
            if (hotFilePaths.Contains(l2Hit.Chunk.FilePath))
            {
                // Check if this specific chunk is already in results
                var existingChunk = merged.FirstOrDefault(m =>
                    m.Chunk.FilePath == l2Hit.Chunk.FilePath &&
                    m.Chunk.StartLine == l2Hit.Chunk.StartLine);

                if (existingChunk != null)
                {
                    // Already have this chunk from L1, skip
                    continue;
                }

                // File is hot but this chunk isn't in L1 results
                // Try to get fresh content from hot cache
                var cachedFile = hotCache.Get(l2Hit.Chunk.FilePath);
                if (cachedFile != null)
                {
                    // Find matching chunk in cached file
                    var freshChunk = cachedFile.Chunks.FirstOrDefault(c =>
                        c.StartLine == l2Hit.Chunk.StartLine);

                    if (freshChunk != null)
                    {
                        merged.Add(new UnifiedSearchHit
                        {
                            Chunk = freshChunk,
                            Score = l2Hit.Score,
                            Source = SearchSource.L2WithL1Content, // L2 score, L1 content
                            IsFresh = true,
                            CachedAt = cachedFile.CachedAt
                        });
                        continue;
                    }
                }
            }

            // Add L2 result as-is
            merged.Add(l2Hit);
        }

        // Sort by score and take top results
        return merged
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}

/// <summary>
/// Result of a unified search across L1 and L2.
/// </summary>
public class UnifiedSearchResult
{
    public required string Query { get; init; }
    public required List<UnifiedSearchHit> Results { get; init; }
    public required int L1HitCount { get; init; }
    public required int L2HitCount { get; init; }
    public required int TotalResultCount { get; init; }
    public required TimeSpan Duration { get; init; }
    public required int HotFilesSearched { get; init; }
}

/// <summary>
/// A single search hit with source information.
/// </summary>
public class UnifiedSearchHit
{
    public required CodeChunk Chunk { get; init; }
    public required float Score { get; init; }
    public required SearchSource Source { get; init; }
    public required bool IsFresh { get; init; }
    public DateTime? CachedAt { get; init; }
}

/// <summary>
/// Where a search result came from.
/// </summary>
public enum SearchSource
{
    /// <summary>
    /// Result from L1 hot cache (always fresh).
    /// </summary>
    L1HotCache,

    /// <summary>
    /// Result from L2 Qdrant (may be stale for hot files).
    /// </summary>
    L2Qdrant,

    /// <summary>
    /// Score from L2, but content replaced with fresh L1 content.
    /// </summary>
    L2WithL1Content
}
