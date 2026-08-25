using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Mcp.Common.Core;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using CodeAssistMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CodeAssistMcp.McpTools;

/// <summary>
/// MCP tools for semantic code search.
/// Uses L1 (hot cache) + L2 (Qdrant) unified search for always-fresh results.
/// </summary>
[McpServerToolType]
public class SearchTools(
    RepositoryIndexer indexer,
    UnifiedSearchService unifiedSearch,
    QdrantService qdrantService,
    HotCache hotCache,
    FileWatcherService fileWatcher,
    L2PromotionService l2Promotion,
    ActiveRepositoryStore activeRepositoryStore,
    RepositoryWatcherStartupService watcherStartup,
    ILogger<SearchTools> logger)
{
    /// <summary>
    /// Ensures the repository is being watched for file changes.
    /// </summary>
    private void EnsureWatching(IndexState state)
    {
        bool wasWatching = fileWatcher.IsWatching(state.RootPath);
        fileWatcher.WatchRepository(state.RootPath, state.IncludePatterns, state.ExcludePatterns);
        l2Promotion.RegisterRepositoryCollection(state.RootPath, state.CollectionName);
        activeRepositoryStore.TrySave(state.RepositoryName);

        if (!wasWatching)
        {
            watcherStartup.RequestReconciliation(state, "watch-start");
            logger.LogInformation("Started watching repository at {Path} for L1 cache updates", state.RootPath);
        }
    }

    [McpServerTool, DisplayName("search_code")]
    [Description("Semantic search across an indexed repository. Returns code chunks most similar to the query, with file paths, line numbers, and relevance scores. Use natural language queries like 'function that handles user authentication' or 'error handling for database connections'. Set includeDependencies=true to also return callers and callees of matched symbols.")]
    public async Task<string> SearchCode(
        string repositoryName,
        string query,
        int limit = 10,
        float minScore = 0.5f,
        bool includeDependencies = false,
        string? pathPrefix = null,
        string? language = null,
        bool includeTests = true,
        bool includeDocumentation = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Searching {Repository} for: {Query} (deps={Deps})", repositoryName, query, includeDependencies);

            IndexState? state = await indexer.GetIndexStateAsync(repositoryName, cancellationToken);
            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'. Use index_repository to create one first."
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Ensure we're watching this repository for L1 cache updates
            EnsureWatching(state);

            int resultLimit = Math.Clamp(limit, 1, 100);
            bool hasFilters = !string.IsNullOrWhiteSpace(pathPrefix)
                || !string.IsNullOrWhiteSpace(language)
                || !includeTests
                || !includeDocumentation;
            Func<CodeChunk, bool>? resultFilter = hasFilters
                ? chunk => MatchesFilters(
                    chunk, pathPrefix, language, includeTests, includeDocumentation)
                : null;
            UnifiedSearchResult response = await unifiedSearch.SearchAsync(
                query, state.CollectionName, state.RootPath, resultLimit, minScore,
                includeDependencies, resultFilter, cancellationToken);

            List<UnifiedSearchHit> filteredHits = response.Results
                .Where(hit => MatchesFilters(
                    hit.Chunk, pathPrefix, language, includeTests, includeDocumentation))
                .Take(resultLimit)
                .ToList();
            var results = filteredHits.Select(FormatHit).ToList();

            object? dependencies = null;
            if (response.DependencyResults is { Count: > 0 })
            {
                dependencies = response.DependencyResults
                    .Where(r => MatchesFilters(
                        r.Chunk, pathPrefix, language, includeTests, includeDocumentation))
                    .Take(50)
                    .Select(r => new
                {
                    filePath = r.Chunk.RelativePath,
                    startLine = r.Chunk.StartLine,
                    endLine = r.Chunk.EndLine,
                    chunkType = r.Chunk.ChunkType,
                    symbolName = r.Chunk.SymbolName,
                    parentSymbol = r.Chunk.ParentSymbol,
                    language = r.Chunk.Language,
                    content = r.Chunk.Content,
                    dependencyType = r.DependencyType
                }).ToList();
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                query,
                repositoryName,
                resultCount = results.Count,
                duration = response.Duration.ToString(),
                l1HitCount = response.L1HitCount,
                l2HitCount = response.L2HitCount,
                hotFilesSearched = response.HotFilesSearched,
                candidateCount = response.CandidateCount,
                dependencySeedCount = response.DependencySeedCount,
                reconciliation = watcherStartup.GetStatus(state.RootPath),
                results,
                dependencies
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching {Repository} for: {Query}", repositoryName, query);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    private static object FormatHit(UnifiedSearchHit r) => new
    {
        filePath = r.Chunk.RelativePath,
        startLine = r.Chunk.StartLine,
        endLine = r.Chunk.EndLine,
        chunkType = r.Chunk.ChunkType,
        symbolName = r.Chunk.SymbolName,
        parentSymbol = r.Chunk.ParentSymbol,
        language = r.Chunk.Language,
        score = Math.Round(r.Score, 4),
        content = r.Chunk.Content,
        source = r.Source.ToString(),
        isFresh = r.IsFresh,
        callsOut = r.Chunk.CallsOut
    };

    [McpServerTool, DisplayName("find_similar_code")]
    [Description("Find code similar to a given code snippet. Useful for finding duplicates, related implementations, or understanding patterns used elsewhere in the codebase.")]
    public async Task<string> FindSimilarCode(
        string repositoryName,
        string codeSnippet,
        int limit = 5,
        float minScore = 0.6f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Finding similar code in {Repository}", repositoryName);

            IndexState? state = await indexer.GetIndexStateAsync(repositoryName, cancellationToken);
            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'. Use index_repository to create one first."
                }, SerializerOptions.JsonOptionsIndented);
            }

            EnsureWatching(state);

            UnifiedSearchResult response = await unifiedSearch.SearchAsync(
                codeSnippet, state.CollectionName, state.RootPath, limit, minScore,
                cancellationToken: cancellationToken);

            var results = response.Results.Select(FormatHit).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                resultCount = results.Count,
                duration = response.Duration.ToString(),
                l1HitCount = response.L1HitCount,
                l2HitCount = response.L2HitCount,
                reconciliation = watcherStartup.GetStatus(state.RootPath),
                results
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding similar code in {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("search_by_symbol")]
    [Description("Search for code by symbol name (class, method, property, etc.). Combines semantic search with the symbol name for more targeted results.")]
    public async Task<string> SearchBySymbol(
        string repositoryName,
        string symbolName,
        string? symbolType = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            IndexState? state = await indexer.GetIndexStateAsync(repositoryName, cancellationToken);
            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'. Use index_repository to create one first."
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Build a query that emphasizes the symbol
            string query = symbolType != null
                ? $"{symbolType} named {symbolName}"
                : $"symbol named {symbolName}";

            logger.LogDebug("Searching {Repository} for symbol: {Symbol}", repositoryName, symbolName);

            EnsureWatching(state);

            int resultLimit = Math.Clamp(limit, 1, 50);
            Task<List<SearchResult>> exactTask = qdrantService.SearchBySymbolNamesAsync(
                state.CollectionName, [symbolName], cancellationToken);
            Task<UnifiedSearchResult> semanticTask = unifiedSearch.SearchAsync(
                query, state.CollectionName, state.RootPath, Math.Min(resultLimit * 2, 100), 0.3f,
                cancellationToken: cancellationToken);
            await Task.WhenAll(exactTask, semanticTask);

            List<SearchResult> indexedExactMatches = await exactTask;
            List<ExactSymbolMatch> exactMatches = SearchResultDiversifier.ResolveFreshExactMatches(
                indexedExactMatches, symbolName, hotCache.Get);
            UnifiedSearchResult response = await semanticTask;
            var ranked = new List<SymbolSearchHit>();
            var seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSplitSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ExactSymbolMatch exact in exactMatches
                         .Where(result => MatchesSymbolType(result.Chunk, symbolType))
                         .OrderBy(result => result.Chunk.RelativePath, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(result => result.Chunk.StartLine))
            {
                string locationKey = GetLocationKey(exact.Chunk);
                if (!seenLocations.Add(locationKey)) continue;

                string? splitKey = GetSplitSymbolKey(exact.Chunk);
                if (splitKey != null && !seenSplitSymbols.Add(splitKey)) continue;

                ranked.Add(new SymbolSearchHit(
                    exact.Chunk,
                    1f,
                    exact.IsFresh ? "ExactSymbolWithL1Content" : "ExactSymbol",
                    exact.IsFresh,
                    "exact"));
            }

            foreach (UnifiedSearchHit semantic in response.Results.Where(result =>
                         MatchesSymbolType(result.Chunk, symbolType)
                         && (result.Chunk.SymbolName?.Contains(symbolName, StringComparison.OrdinalIgnoreCase) == true
                             || result.Chunk.Content.Contains(symbolName, StringComparison.OrdinalIgnoreCase))))
            {
                if (!seenLocations.Add(GetLocationKey(semantic.Chunk))) continue;
                ranked.Add(new SymbolSearchHit(
                    semantic.Chunk, semantic.Score, semantic.Source.ToString(), semantic.IsFresh, "semantic"));
            }

            var results = ranked.Take(resultLimit).Select(r => new
                {
                    filePath = r.Chunk.RelativePath,
                    startLine = r.Chunk.StartLine,
                    endLine = r.Chunk.EndLine,
                    chunkType = r.Chunk.ChunkType,
                    symbolName = r.Chunk.SymbolName,
                    parentSymbol = r.Chunk.ParentSymbol,
                    language = r.Chunk.Language,
                    score = Math.Round(r.Score, 4),
                    content = r.Chunk.Content,
                    source = r.Source,
                    isFresh = r.IsFresh,
                    matchType = r.MatchType
                }).ToList();

            stopwatch.Stop();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                searchedSymbol = symbolName,
                symbolType,
                resultCount = results.Count,
                duration = stopwatch.Elapsed.ToString(),
                exactMatchCount = exactMatches.Count,
                indexedExactMatchCount = indexedExactMatches.Count,
                semanticCandidateCount = response.Results.Count,
                l1HitCount = response.L1HitCount,
                l2HitCount = response.L2HitCount,
                reconciliation = watcherStartup.GetStatus(state.RootPath),
                results
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching for symbol {Symbol} in {Repository}", symbolName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    private static bool MatchesSymbolType(CodeChunk chunk, string? symbolType)
    {
        return string.IsNullOrWhiteSpace(symbolType)
            || SearchResultDiversifier.BaseChunkType(chunk.ChunkType)
                .Equals(symbolType, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLocationKey(CodeChunk chunk) =>
        $"{chunk.RelativePath}:{chunk.StartLine}:{chunk.EndLine}";

    private static string? GetSplitSymbolKey(CodeChunk chunk)
    {
        string baseType = SearchResultDiversifier.BaseChunkType(chunk.ChunkType);
        if (baseType.Equals(chunk.ChunkType, StringComparison.Ordinal)) return null;

        string symbol = chunk.ParentSymbol ?? chunk.SymbolName ?? "";
        return $"{chunk.RelativePath}:{baseType}:{symbol}";
    }

    private sealed record SymbolSearchHit(
        CodeChunk Chunk,
        float Score,
        string Source,
        bool IsFresh,
        string MatchType);

    [McpServerTool, DisplayName("explain_code_area")]
    [Description("Retrieve representative code areas related to a concept. Returns code locations and context; it does not generate a narrative explanation. Documentation and tests are excluded by default but can be included explicitly.")]
    public async Task<string> ExplainCodeArea(
        string repositoryName,
        string concept,
        int limit = 5,
        bool includeTests = false,
        bool includeDocumentation = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IndexState? state = await indexer.GetIndexStateAsync(repositoryName, cancellationToken);
            if (state == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No index found for repository '{repositoryName}'. Use index_repository to create one first."
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogDebug("Explaining code area for '{Concept}' in {Repository}", concept, repositoryName);

            EnsureWatching(state);

            int resultLimit = Math.Clamp(limit, 1, 50);
            UnifiedSearchResult response = await unifiedSearch.SearchAsync(
                concept, state.CollectionName, state.RootPath, resultLimit, 0.4f,
                resultFilter: chunk => MatchesFilters(
                    chunk, null, null, includeTests, includeDocumentation),
                cancellationToken: cancellationToken);

            var areas = response.Results
                .Where(r => MatchesFilters(
                    r.Chunk, null, null, includeTests, includeDocumentation))
                .Take(resultLimit)
                .Select(r => new
            {
                filePath = r.Chunk.RelativePath,
                location = $"Lines {r.Chunk.StartLine}-{r.Chunk.EndLine}",
                chunkType = r.Chunk.ChunkType,
                symbolName = r.Chunk.SymbolName,
                parentSymbol = r.Chunk.ParentSymbol,
                relevanceScore = Math.Round(r.Score, 4),
                code = r.Chunk.Content,
                source = r.Source.ToString(),
                isFresh = r.IsFresh
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                concept,
                repositoryName,
                areasFound = areas.Count,
                duration = response.Duration.ToString(),
                l1HitCount = response.L1HitCount,
                l2HitCount = response.L2HitCount,
                reconciliation = watcherStartup.GetStatus(state.RootPath),
                codeAreas = areas
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error explaining code area for '{Concept}' in {Repository}", concept, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    private static bool MatchesFilters(
        CodeChunk chunk,
        string? pathPrefix,
        string? language,
        bool includeTests,
        bool includeDocumentation)
    {
        string normalizedPath = chunk.RelativePath.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(pathPrefix)
            && !normalizedPath.StartsWith(
                pathPrefix.Replace('\\', '/').TrimStart('/'),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(language)
            && !chunk.Language.Equals(language, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!includeTests
            && IndexPath.IsTestPath(normalizedPath))
        {
            return false;
        }

        return includeDocumentation
            || chunk.Language is not ("markdown" or "text")
            && !normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }
}
