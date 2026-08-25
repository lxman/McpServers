using System.Diagnostics;
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
    ISemanticSearchBackend qdrantService,
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
        string repositoryRoot,
        int limit = 10,
        float minScore = 0.5f,
        bool includeDependencies = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Generate query embedding once
        float[] queryEmbedding = await embeddingService.GetEmbeddingAsync(query, cancellationToken);

        // Search L1 (hot cache) - always fresh
        List<UnifiedSearchHit> l1Results = SearchL1(
            queryEmbedding, repositoryRoot, limit, minScore, cancellationToken);

        // Search L2 (Qdrant) - full codebase
        Task<List<UnifiedSearchHit>> l2Task = SearchL2Async(
            qdrantService, collectionName, queryEmbedding, limit, minScore, cancellationToken);

        List<UnifiedSearchHit> l2Results = await l2Task;

        // Merge results with L1 priority
        List<UnifiedSearchHit> mergedResults = MergeResults(l1Results, l2Results, limit);

        // Expand dependencies if requested
        List<UnifiedSearchHit>? dependencyResults = null;
        if (includeDependencies && mergedResults.Count > 0)
        {
            dependencyResults = await ExpandDependenciesAsync(collectionName, mergedResults, cancellationToken);
        }

        stopwatch.Stop();

        var result = new UnifiedSearchResult
        {
            Query = query,
            Results = mergedResults,
            DependencyResults = dependencyResults,
            L1HitCount = l1Results.Count,
            L2HitCount = l2Results.Count,
            TotalResultCount = mergedResults.Count,
            Duration = stopwatch.Elapsed,
            HotFilesSearched = hotCache.CountForRepository(repositoryRoot)
        };

        logger.LogDebug(
            "Unified search completed in {Duration}ms: L1={L1Count}, L2={L2Count}, Merged={MergedCount}, Deps={DepCount}",
            stopwatch.ElapsedMilliseconds, l1Results.Count, l2Results.Count, mergedResults.Count,
            dependencyResults?.Count ?? 0);

        return result;
    }

    private List<UnifiedSearchHit> SearchL1(
        float[] queryEmbedding,
        string repositoryRoot,
        int limit,
        float minScore,
        CancellationToken cancellationToken)
    {
        try
        {
            List<HotCacheSearchResult> l1Results = hotCache.Search(
                queryEmbedding,
                repositoryRoot,
                limit,
                minScore,
                cancellationToken);

            return l1Results.Select(r => new UnifiedSearchHit
            {
                Chunk = r.Chunk,
                Score = r.Score,
                Source = SearchSource.L1HotCache,
                IsFresh = true,
                CachedAt = r.CachedFile.CachedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException) throw;
            logger.LogWarning(ex, "L1 search failed, continuing with L2 only");
            return [];
        }
    }

    internal static async Task<List<UnifiedSearchHit>> SearchL2Async(
        ISemanticSearchBackend qdrantService,
        string collectionName,
        float[] queryEmbedding,
        int limit,
        float minScore,
        CancellationToken cancellationToken)
    {
        List<SearchResult> l2Results = await qdrantService.SearchAsync(
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
            IsFresh = false,
            CachedAt = null
        }).ToList();
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
        foreach (UnifiedSearchHit l2Hit in l2Results)
        {
            // Skip if we already have this file from L1 (L1 has fresher content)
            if (hotFilePaths.Contains(l2Hit.Chunk.FilePath))
            {
                // Check if this specific chunk is already in results
                UnifiedSearchHit? existingChunk = merged.FirstOrDefault(m =>
                    m.Chunk.FilePath == l2Hit.Chunk.FilePath &&
                    m.Chunk.StartLine == l2Hit.Chunk.StartLine);

                if (existingChunk != null)
                {
                    // Already have this chunk from L1, skip
                    continue;
                }

                // File is hot but this chunk isn't in L1 results
                // Try to get fresh content from hot cache
                CachedFile? cachedFile = hotCache.Get(l2Hit.Chunk.FilePath);
                if (cachedFile != null)
                {
                    // Find matching chunk in cached file
                    CodeChunk? freshChunk = cachedFile.Chunks.FirstOrDefault(c =>
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

    private async Task<List<UnifiedSearchHit>> ExpandDependenciesAsync(
        string collectionName,
        List<UnifiedSearchHit> primaryHits,
        CancellationToken cancellationToken)
    {
        // Collect all calls_out from primary hits (callees to look up)
            var calleeNames = new HashSet<string>(StringComparer.Ordinal);
            // Collect all symbol names from primary hits (to find callers of)
            var primarySymbols = new HashSet<string>(StringComparer.Ordinal);
            var primaryIds = new HashSet<Guid>();

            foreach (UnifiedSearchHit hit in primaryHits)
            {
                primaryIds.Add(hit.Chunk.Id);

                if (hit.Chunk.CallsOut is { Count: > 0 })
                {
                    foreach (CallReference call in hit.Chunk.CallsOut)
                        calleeNames.Add(call.MethodName);
                }

                if (!string.IsNullOrEmpty(hit.Chunk.SymbolName))
                    primarySymbols.Add(hit.Chunk.SymbolName);
            }

            var depResults = new List<UnifiedSearchHit>();

            // Find callee definitions (chunks whose symbol_name matches a calls_out entry)
            if (calleeNames.Count > 0)
            {
                List<SearchResult> callees = await qdrantService.SearchBySymbolNamesAsync(
                    collectionName, calleeNames.ToList(), cancellationToken);

                foreach (SearchResult r in callees)
                {
                    if (primaryIds.Contains(r.Chunk.Id)) continue;
                    depResults.Add(new UnifiedSearchHit
                    {
                        Chunk = r.Chunk,
                        Score = r.Score,
                        Source = SearchSource.DependencyGraph,
                        IsFresh = false,
                        DependencyType = "callee"
                    });
                }
            }

            // Find callers (chunks whose calls_out contains a primary symbol)
            foreach (string symbol in primarySymbols)
            {
                List<SearchResult> callers = await qdrantService.SearchCallersOfAsync(
                    collectionName, symbol, cancellationToken);

                foreach (SearchResult r in callers)
                {
                    if (primaryIds.Contains(r.Chunk.Id)) continue;
                    // Avoid duplicates in dependency results
                    if (depResults.Any(d => d.Chunk.Id == r.Chunk.Id)) continue;
                    depResults.Add(new UnifiedSearchHit
                    {
                        Chunk = r.Chunk,
                        Score = r.Score,
                        Source = SearchSource.DependencyGraph,
                        IsFresh = false,
                        DependencyType = "caller"
                    });
                }
            }

        return depResults;
    }

}

/// <summary>
/// Result of a unified search across L1 and L2.
/// </summary>
public class UnifiedSearchResult
{
    public required string Query { get; init; }
    public required List<UnifiedSearchHit> Results { get; init; }
    public List<UnifiedSearchHit>? DependencyResults { get; init; }
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
    public string? DependencyType { get; init; }
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
    L2WithL1Content,

    /// <summary>
    /// Result from dependency graph expansion (caller/callee lookup).
    /// </summary>
    DependencyGraph
}
