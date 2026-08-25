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
        Func<CodeChunk, bool>? resultFilter = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int requestedLimit = Math.Clamp(limit, 1, 100);
        int candidateLimit = resultFilter == null
            ? Math.Min(requestedLimit * 3, 100)
            : Math.Min(Math.Max(requestedLimit * 20, 100), 500);

        // Generate query embedding once
        float[] queryEmbedding = await embeddingService.GetEmbeddingAsync(query, cancellationToken);

        // Search L1 (hot cache) - always fresh
        List<UnifiedSearchHit> l1Results = SearchL1(
            queryEmbedding, repositoryRoot, candidateLimit, minScore, cancellationToken);

        // Search L2 (Qdrant) - full codebase
        Task<List<UnifiedSearchHit>> l2Task = SearchL2Async(
            qdrantService, collectionName, queryEmbedding, candidateLimit, minScore, cancellationToken);

        List<UnifiedSearchHit> l2Results = await l2Task;

        if (resultFilter != null)
        {
            l1Results = l1Results.Where(hit => resultFilter(hit.Chunk)).ToList();
            l2Results = l2Results.Where(hit => resultFilter(hit.Chunk)).ToList();
        }

        // Merge results with L1 priority
        List<UnifiedSearchHit> mergedCandidates = MergeResults(
            l1Results, l2Results, candidateLimit, hotCache.Get);
        List<UnifiedSearchHit> mergedResults = SearchResultDiversifier.Diversify(mergedCandidates, requestedLimit);

        // Expand dependencies if requested
        List<UnifiedSearchHit>? dependencyResults = null;
        var dependencySeedCount = 0;
        if (includeDependencies && mergedResults.Count > 0)
        {
            // Search results are deliberately diversified, so lower-ranked hits often represent
            // different concepts or files. Expanding every one made a focused query return the call
            // graph of unrelated secondary matches. The top-ranked hit is the dependency seed; the
            // dedicated trace tool remains available when callers want a broader graph traversal.
            List<UnifiedSearchHit> dependencySeeds = mergedResults.Take(1).ToList();
            dependencySeedCount = dependencySeeds.Count;
            dependencyResults = await ExpandDependenciesAsync(
                collectionName,
                dependencySeeds,
                mergedResults.Select(hit => hit.Chunk.Id).ToHashSet(),
                cancellationToken);
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
            CandidateCount = mergedCandidates.Count,
            DependencySeedCount = dependencySeedCount,
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

    internal static List<UnifiedSearchHit> MergeResults(
        List<UnifiedSearchHit> l1Results,
        List<UnifiedSearchHit> l2Results,
        int limit,
        Func<string, CachedFile?> hotCacheLookup)
    {
        var merged = new List<UnifiedSearchHit>();

        // Add all L1 results (always fresh)
        merged.AddRange(l1Results);

        // Add L2 results, but replace content for hot files
        foreach (UnifiedSearchHit l2Hit in l2Results)
        {
            // The cache itself, rather than the set of L1 results, is authoritative for whether a
            // file is hot. A changed file may have no L1 hit for this query while its stale L2
            // content still scores highly. Returning that payload would violate the freshness
            // guarantee precisely when a rename or deletion removes the matching text.
            CachedFile? cachedFile = string.IsNullOrWhiteSpace(l2Hit.Chunk.FilePath)
                ? null
                : hotCacheLookup(l2Hit.Chunk.FilePath);
            if (cachedFile != null)
            {
                // Check if this specific chunk is already in results
                UnifiedSearchHit? existingChunk = merged.FirstOrDefault(m =>
                    m.Chunk.FilePath.Equals(l2Hit.Chunk.FilePath, StringComparison.OrdinalIgnoreCase) &&
                    m.Chunk.StartLine == l2Hit.Chunk.StartLine);

                if (existingChunk != null)
                {
                    // Already have this chunk from L1, skip
                    continue;
                }

                // File is hot but this chunk isn't in L1 results
                // Try to get fresh content from hot cache
                CodeChunk? freshChunk = cachedFile.Chunks.FirstOrDefault(c =>
                    c.StartLine == l2Hit.Chunk.StartLine);

                if (freshChunk != null)
                {
                    merged.Add(new UnifiedSearchHit
                    {
                        Chunk = freshChunk,
                        Score = l2Hit.Score,
                        Source = SearchSource.L2WithL1Content,
                        IsFresh = true,
                        CachedAt = cachedFile.CachedAt
                    });
                }

                // If the old location no longer maps to a current chunk, the symbol/content was
                // removed or moved. Drop the stale hit; L1 remains responsible for discovering its
                // replacement under the new content and line layout.
                continue;
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
        List<UnifiedSearchHit> dependencySeeds,
        HashSet<Guid> allPrimaryIds,
        CancellationToken cancellationToken)
    {
        var dependencyIds = new HashSet<Guid>();
        var dependencies = new List<UnifiedSearchHit>();

        List<string> qualifiedNames = BuildQualifiedCalleeNames(dependencySeeds);
        if (qualifiedNames.Count > 0)
        {
            List<SearchResult> qualifiedCallees = await qdrantService.SearchByQualifiedNamesAsync(
                collectionName, qualifiedNames, cancellationToken);
            AddDependencies(qualifiedCallees, "callee", allPrimaryIds, dependencyIds, dependencies);
        }

        foreach (UnifiedSearchHit primary in dependencySeeds.Where(hit =>
                     !string.IsNullOrEmpty(hit.Chunk.SymbolName)
                     && !string.IsNullOrEmpty(hit.Chunk.QualifiedName)))
        {
            string symbol = SearchResultDiversifier.RemovePartSuffix(primary.Chunk.SymbolName!);
            string qualifiedName = SearchResultDiversifier.RemovePartSuffix(primary.Chunk.QualifiedName!);
            List<SearchResult> callers = await qdrantService.SearchCallersOfAsync(
                collectionName, symbol, cancellationToken);
            IEnumerable<SearchResult> verifiedCallers = callers.Where(result =>
                result.Chunk.CallsOut?.Any(call =>
                    call.MethodName.Equals(symbol, StringComparison.Ordinal)
                    && call.QualifiedName is { Length: > 0 } callName
                    && SearchResultDiversifier.RemovePartSuffix(callName)
                        .Equals(qualifiedName, StringComparison.OrdinalIgnoreCase)) == true);
            AddDependencies(verifiedCallers, "caller", allPrimaryIds, dependencyIds, dependencies);
        }

        return ConsolidateDependencyFragments(dependencies).Take(50).ToList();
    }

    internal static List<string> BuildQualifiedCalleeNames(IEnumerable<UnifiedSearchHit> primaryHits)
    {
        return primaryHits
            .SelectMany(hit => (hit.Chunk.CallsOut ?? [])
                .Select(call => ResolveQualifiedCalleeName(hit.Chunk, call)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? ResolveQualifiedCalleeName(CodeChunk source, CallReference call)
    {
        if (call.QualifiedName is { Length: > 0 })
            return SearchResultDiversifier.RemovePartSuffix(call.QualifiedName);

        if (call.ReceiverType is { Length: > 0 })
            return $"{call.ReceiverType}.{call.MethodName}";

        // Match the graph builder's conservative rule: only a genuinely unqualified call may be
        // inferred as another member on the enclosing type. Requiring ParentSymbol avoids treating
        // calls collected on a class/file aggregate as if its qualified name were a callable.
        if (!string.IsNullOrEmpty(call.ReceiverExpression)
            || string.IsNullOrEmpty(source.ParentSymbol)
            || source.QualifiedName is not { Length: > 0 } sourceQualifiedName)
        {
            return null;
        }

        string canonicalSource = SearchResultDiversifier.RemovePartSuffix(sourceQualifiedName);
        int memberSeparator = canonicalSource.LastIndexOf('.');
        return memberSeparator > 0
            ? $"{canonicalSource[..memberSeparator]}.{call.MethodName}"
            : null;
    }

    internal static List<UnifiedSearchHit> ConsolidateDependencyFragments(
        IEnumerable<UnifiedSearchHit> dependencies)
    {
        List<UnifiedSearchHit> source = dependencies.ToList();
        Dictionary<string, List<UnifiedSearchHit>> partGroups = source
            .Where(hit => SearchResultDiversifier.BaseChunkType(hit.Chunk.ChunkType)
                          != hit.Chunk.ChunkType)
            .GroupBy(LogicalDependencyKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var emittedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consolidated = new List<UnifiedSearchHit>(source.Count);
        foreach (UnifiedSearchHit hit in source)
        {
            if (SearchResultDiversifier.BaseChunkType(hit.Chunk.ChunkType) == hit.Chunk.ChunkType)
            {
                consolidated.Add(hit);
                continue;
            }

            string key = LogicalDependencyKey(hit);
            if (!emittedGroups.Add(key)) continue;
            consolidated.Add(CombineDependencyParts(partGroups[key]));
        }

        return consolidated;
    }

    private static string LogicalDependencyKey(UnifiedSearchHit hit)
    {
        CodeChunk chunk = hit.Chunk;
        string symbol = chunk.QualifiedName is { Length: > 0 } qualifiedName
            ? SearchResultDiversifier.RemovePartSuffix(qualifiedName)
            : $"{chunk.RelativePath}|{SearchResultDiversifier.RemovePartSuffix(
                chunk.SymbolName ?? chunk.ParentSymbol ?? chunk.Id.ToString())}";
        return $"{hit.DependencyType}|{symbol}";
    }

    private static UnifiedSearchHit CombineDependencyParts(List<UnifiedSearchHit> parts)
    {
        List<UnifiedSearchHit> ordered = parts
            .OrderBy(hit => hit.Chunk.StartLine)
            .ThenBy(hit => hit.Chunk.EndLine)
            .ToList();
        UnifiedSearchHit first = ordered[0];
        string? canonicalQualifiedName = first.Chunk.QualifiedName is { Length: > 0 } qualifiedName
            ? SearchResultDiversifier.RemovePartSuffix(qualifiedName)
            : null;
        string? containingSymbol = canonicalQualifiedName is null
            ? first.Chunk.ParentSymbol
            : ContainingSymbol(canonicalQualifiedName);

        CodeChunk combinedChunk = first.Chunk with
        {
            StartLine = ordered.Min(hit => hit.Chunk.StartLine),
            EndLine = ordered.Max(hit => hit.Chunk.EndLine),
            ChunkType = SearchResultDiversifier.BaseChunkType(first.Chunk.ChunkType),
            SymbolName = first.Chunk.SymbolName is { Length: > 0 } symbolName
                ? SearchResultDiversifier.RemovePartSuffix(symbolName)
                : first.Chunk.SymbolName,
            ParentSymbol = containingSymbol,
            QualifiedName = canonicalQualifiedName,
            Content = MergePartContent(ordered.Select(hit => hit.Chunk)),
            CallsOut = ordered
                .SelectMany(hit => hit.Chunk.CallsOut ?? [])
                .Distinct()
                .OrderBy(call => call.Line)
                .ToList()
        };

        return new UnifiedSearchHit
        {
            Chunk = combinedChunk,
            Score = ordered.Max(hit => hit.Score),
            Source = SearchSource.DependencyGraph,
            IsFresh = ordered.Any(hit => hit.IsFresh),
            CachedAt = ordered.Max(hit => hit.CachedAt),
            DependencyType = first.DependencyType
        };
    }

    private static string? ContainingSymbol(string qualifiedName)
    {
        int memberSeparator = qualifiedName.LastIndexOf('.');
        if (memberSeparator <= 0) return null;
        string containingName = qualifiedName[..memberSeparator];
        int typeSeparator = containingName.LastIndexOf('.');
        return typeSeparator >= 0 ? containingName[(typeSeparator + 1)..] : containingName;
    }

    private static string MergePartContent(IEnumerable<CodeChunk> chunks)
    {
        var mergedLines = new List<string>();
        int? lastEndLine = null;
        foreach (CodeChunk chunk in chunks)
        {
            string[] lines = chunk.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            int overlap = lastEndLine is null ? 0 : Math.Max(0, lastEndLine.Value - chunk.StartLine + 1);
            mergedLines.AddRange(lines.Skip(Math.Min(overlap, lines.Length)));
            lastEndLine = Math.Max(lastEndLine ?? chunk.EndLine, chunk.EndLine);
        }

        return string.Join(Environment.NewLine, mergedLines);
    }

    private static void AddDependencies(
        IEnumerable<SearchResult> results,
        string dependencyType,
        HashSet<Guid> primaryIds,
        HashSet<Guid> dependencyIds,
        List<UnifiedSearchHit> dependencies)
    {
        foreach (SearchResult result in results)
        {
            if (primaryIds.Contains(result.Chunk.Id) || !dependencyIds.Add(result.Chunk.Id)) continue;
            dependencies.Add(new UnifiedSearchHit
            {
                Chunk = result.Chunk,
                Score = result.Score,
                Source = SearchSource.DependencyGraph,
                IsFresh = false,
                DependencyType = dependencyType
            });
        }
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
    public required int CandidateCount { get; init; }
    public required int DependencySeedCount { get; init; }
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
