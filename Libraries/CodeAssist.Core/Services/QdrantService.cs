using CodeAssist.Core.Configuration;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace CodeAssist.Core.Services;

/// <summary>
/// Service for vector storage operations using Qdrant.
/// Uses lazy client initialization with reconnection support.
/// </summary>
public sealed class QdrantService : IQdrantWriter, ISemanticSearchBackend
{
    public event Action<string>? CollectionChanged;

    /// <summary>
    /// Default ceiling for the graph-query helpers. Before paging they were implicitly capped at one
    /// scroll page; without a cap the first caller of one of these would page an entire namespace's
    /// chunks — content included — into memory. Public so a caller can compare its result count
    /// against it: getting back exactly this many results means the cap was hit and more may exist.
    /// </summary>
    public const int DefaultGraphQueryLimit = 1000;

    private QdrantClient? _client;
    private readonly object _clientLock = new();
    private DateTime _lastFailedAttempt = DateTime.MinValue;
    private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromSeconds(30);

    private readonly CodeAssistOptions _options;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(
        IOptions<CodeAssistOptions> options,
        ILogger<QdrantService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Get or create the Qdrant client. Enforces a cooldown between failed connection attempts
    /// to prevent thrashing when the server is genuinely down.
    /// </summary>
    private QdrantClient GetClient()
    {
        if (_client != null) return _client;

        lock (_clientLock)
        {
            if (_client != null) return _client;

            TimeSpan sinceLast = DateTime.UtcNow - _lastFailedAttempt;
            if (sinceLast < ReconnectCooldown)
            {
                throw new InvalidOperationException(
                    $"Qdrant connection failed recently. Retry in {(ReconnectCooldown - sinceLast).Seconds}s, " +
                    $"or call check_health to force a reconnection attempt.");
            }

            try
            {
                var uri = new Uri(_options.QdrantUrl);
                int grpcPort = uri.Port == 6333 ? 6334 : uri.Port;

                _client = new QdrantClient(
                    host: uri.Host,
                    port: grpcPort,
                    https: uri.Scheme == "https");

                _logger.LogInformation("Created Qdrant client for {Host}:{Port}", uri.Host, grpcPort);
                return _client;
            }
            catch (Exception ex)
            {
                _lastFailedAttempt = DateTime.UtcNow;
                _logger.LogError(ex, "Failed to create Qdrant client");
                throw;
            }
        }
    }

    /// <summary>
    /// Reset the connection, disposing the current client. The next operation will create a fresh client.
    /// Called by the health check tool to recover from stale gRPC channel state.
    /// </summary>
    public void ResetConnection()
    {
        lock (_clientLock)
        {
            _client?.Dispose();
            _client = null;
            _lastFailedAttempt = DateTime.MinValue; // Clear cooldown so next attempt proceeds immediately
            _logger.LogInformation("Qdrant connection reset — next operation will reconnect");
        }
    }

    /// <summary>
    /// Ensure a collection exists with the correct configuration.
    /// </summary>
    public async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            bool exists = await GetClient().CollectionExistsAsync(collectionName, cancellationToken);

            if (!exists)
            {
                _logger.LogInformation("Creating collection {Collection}", collectionName);

                await GetClient().CreateCollectionAsync(
                    collectionName,
                    new VectorParams
                    {
                        Size = (ulong)_options.VectorDimension,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);

            }

            // Also applies schema additions to existing collections during the next refresh.
            await EnsurePayloadIndexesAsync(collectionName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection {Collection}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Upsert code chunks with their embeddings.
    /// </summary>
    public async Task UpsertChunksAsync(
        string collectionName,
        IReadOnlyList<CodeChunk> chunks,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count != embeddings.Count)
        {
            throw new ArgumentException("Chunks and embeddings count must match");
        }

        if (chunks.Count == 0) return;

        try
        {
            List<PointStruct> points = chunks.Select((chunk, i) => new PointStruct
            {
                Id = new PointId { Uuid = chunk.Id.ToString() },
                Vectors = embeddings[i],
                Payload =
                {
                    ["file_path"] = chunk.FilePath,
                    ["relative_path"] = chunk.RelativePath,
                    ["content"] = chunk.Content,
                    ["start_line"] = chunk.StartLine,
                    ["end_line"] = chunk.EndLine,
                    ["chunk_type"] = chunk.ChunkType,
                    ["symbol_name"] = chunk.SymbolName ?? "",
                    ["canonical_symbol_name"] = chunk.SymbolName is { Length: > 0 } symbolName
                        ? SearchResultDiversifier.RemovePartSuffix(symbolName)
                        : "",
                    ["parent_symbol"] = chunk.ParentSymbol ?? "",
                    ["language"] = chunk.Language,
                    ["content_hash"] = chunk.ContentHash,
                    ["calls_out"] = chunk.CallsOut is { Count: > 0 }
                        ? new Value { ListValue = BuildCallReferenceList(chunk.CallsOut) }
                        : new Value { ListValue = new ListValue() },
                    // Denormalized call names for efficient keyword filtering
                    ["calls_out_names"] = chunk.CallsOut is { Count: > 0 }
                        ? new Value { ListValue = BuildStringList(chunk.CallsOut.Select(c => c.MethodName).ToList()) }
                        : new Value { ListValue = new ListValue() },
                    // New Phase 1 fields
                    ["return_type"] = chunk.ReturnType ?? "",
                    ["base_type"] = chunk.BaseType ?? "",
                    ["implemented_interfaces"] = chunk.ImplementedInterfaces is { Count: > 0 }
                        ? new Value { ListValue = BuildStringList(chunk.ImplementedInterfaces.ToList()) }
                        : new Value { ListValue = new ListValue() },
                    ["access_modifier"] = chunk.AccessModifier ?? "",
                    ["modifiers"] = chunk.Modifiers is { Count: > 0 }
                        ? new Value { ListValue = BuildStringList(chunk.Modifiers.ToList()) }
                        : new Value { ListValue = new ListValue() },
                    ["attributes"] = chunk.Attributes is { Count: > 0 }
                        ? new Value { ListValue = BuildStringList(chunk.Attributes.ToList()) }
                        : new Value { ListValue = new ListValue() },
                    ["namespace"] = chunk.Namespace ?? "",
                    ["qualified_name"] = chunk.QualifiedName ?? "",
                    ["canonical_qualified_name"] = chunk.QualifiedName is { Length: > 0 } qualifiedName
                        ? SearchResultDiversifier.RemovePartSuffix(qualifiedName)
                        : "",
                    ["parameters"] = chunk.Parameters is { Count: > 0 }
                        ? new Value { ListValue = BuildParameterList(chunk.Parameters) }
                        : new Value { ListValue = new ListValue() },
                    ["field_accesses"] = chunk.FieldAccesses is { Count: > 0 }
                        ? new Value { ListValue = BuildFieldAccessList(chunk.FieldAccesses) }
                        : new Value { ListValue = new ListValue() }
                }
            }).ToList();

            await GetClient().UpsertAsync(collectionName, points, cancellationToken: cancellationToken);
            NotifyCollectionChanged(collectionName);

            _logger.LogDebug("Upserted {Count} chunks to collection {Collection}", chunks.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert {Count} chunks to collection {Collection}", chunks.Count, collectionName);
            throw;
        }
    }

    /// <summary>
    /// Search for similar code chunks.
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit = 10,
        float minScore = 0.5f,
        string? filePathFilter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Filter? filter = null;
            if (!string.IsNullOrEmpty(filePathFilter))
            {
                filter = BuildRelativePathFilter(filePathFilter);
            }

            IReadOnlyList<ScoredPoint> results = await GetClient().SearchAsync(
                collectionName,
                queryEmbedding,
                limit: (ulong)limit,
                scoreThreshold: minScore,
                filter: filter,
                cancellationToken: cancellationToken);

            return results.Select(r => new SearchResult
            {
                Score = r.Score,
                Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search collection {Collection}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// The one filter used for every <c>relative_path</c> lookup.
    /// </summary>
    /// <remarks>
    /// Deliberately a keyword (exact) match. The previous full-text <c>Match { Text }</c> did not fail on
    /// the unindexed field as was first assumed — Qdrant full-scanned and matched — but it is tokenized,
    /// so it matches far more than intended: the bare token "Editing" matched 1,963 points in a live
    /// collection. A delete built on that can take unrelated files with it.
    /// </remarks>
    internal static Filter BuildRelativePathFilter(string relativePath) => new()
    {
        Must =
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "relative_path",
                    Match = new Match { Keyword = IndexPath.Normalize(relativePath) }
                }
            }
        }
    };

    /// <summary>
    /// Delete chunks by file path.
    /// </summary>
    public async Task DeleteByFilePathAsync(
        string collectionName,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetClient().DeleteAsync(
                collectionName,
                BuildRelativePathFilter(relativePath),
                cancellationToken: cancellationToken);
            NotifyCollectionChanged(collectionName);

            _logger.LogDebug("Deleted chunks for file {FilePath} from collection {Collection}", relativePath, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunks for file {FilePath} from collection {Collection}", relativePath, collectionName);
            throw;
        }
    }

    /// <summary>
    /// Delete a known set of points after their replacements have been stored.
    /// </summary>
    public async Task DeletePointsAsync(
        string collectionName,
        IReadOnlyList<Guid> pointIds,
        CancellationToken cancellationToken = default)
    {
        if (pointIds.Count == 0) return;

        try
        {
            await GetClient().DeleteAsync(
                collectionName,
                pointIds,
                cancellationToken: cancellationToken);
            NotifyCollectionChanged(collectionName);

            _logger.LogDebug("Deleted {Count} points from collection {Collection}",
                pointIds.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Count} points from collection {Collection}",
                pointIds.Count, collectionName);
            throw;
        }
    }

    /// <summary>
    /// Scroll all chunks for a given file path (used by graph rebuild).
    /// </summary>
    /// <remarks>
    /// Normalizes here rather than trusting callers. A keyword match on a Windows-shaped path against
    /// forward-slash rows returns zero results rather than an error, so a caller that forgot would get
    /// a silently empty file instead of a failure — the same quiet-mismatch failure mode this class's
    /// delete path was fixed for.
    /// </remarks>
    public async Task<List<SearchResult>> SearchByFilePathAsync(
        string collectionName,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "relative_path", IndexPath.Normalize(relativePath), cancellationToken);
    }

    /// <summary>
    /// Delete chunks by IDs.
    /// </summary>
    public async Task DeleteByIdsAsync(
        string collectionName,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return;

        try
        {
            List<PointId> pointIds = ids.Select(id => new PointId { Uuid = id.ToString() }).ToList();
            await GetClient().DeleteAsync(collectionName, pointIds, cancellationToken: cancellationToken);

            _logger.LogDebug("Deleted {Count} chunks by ID from collection {Collection}", ids.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Count} chunks by ID from collection {Collection}", ids.Count, collectionName);
            throw;
        }
    }

    /// <summary>
    /// The ids of the points currently stored for a file, without their payloads or vectors.
    /// </summary>
    /// <remarks>
    /// Used to supersede a file's chunks by writing the new generation before removing the old, so a
    /// failed write leaves the file stale rather than absent. Payload and vectors are switched off
    /// deliberately: a file can hold hundreds of chunks and each payload carries its whole source
    /// text, so fetching them to learn a list of GUIDs would move megabytes to no purpose.
    /// </remarks>
    public async Task<List<Guid>> GetPointIdsByFilePathAsync(
        string collectionName,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var ids = new List<Guid>();

        try
        {
            Filter filter = BuildRelativePathFilter(relativePath);
            PointId? offset = null;

            while (true)
            {
                ScrollResponse response = await GetClient().ScrollAsync(
                    collectionName,
                    filter: filter,
                    limit: 1000,
                    offset: offset,
                    payloadSelector: false,
                    vectorsSelector: false,
                    cancellationToken: cancellationToken);

                foreach (RetrievedPoint point in response.Result)
                {
                    if (Guid.TryParse(point.Id.Uuid, out Guid id))
                    {
                        ids.Add(id);
                    }
                }

                // Qdrant signals the last page with a null offset. Guard the contract violation too:
                // an offset that does not advance would otherwise spin here forever, hanging the
                // caller rather than failing it. Mirrors ScrollWithKeywordFilterAsync's guard.
                if (response.NextPageOffset is null) break;
                if (offset is not null && response.NextPageOffset.Equals(offset))
                {
                    _logger.LogWarning(
                        "Scroll offset did not advance while collecting point ids for {FilePath} in "
                        + "{Collection}; stopping after {Count} id(s) to avoid looping.",
                        relativePath, collectionName, ids.Count);
                    break;
                }

                offset = response.NextPageOffset;
            }

            return ids;
        }
        catch (Exception ex)
        {
            // Rethrown rather than returning what was collected so far — a partial id list is
            // indistinguishable from a complete one at the call site, and the caller must be able to
            // tell "this file has no chunks" from "I could not find out".
            _logger.LogError(ex,
                "Failed to collect point ids for {FilePath} in {Collection}", relativePath, collectionName);
            throw;
        }
    }

    /// <summary>
    /// Get collection info.
    /// </summary>
    public async Task<(ulong pointCount, bool exists)> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool exists = await GetClient().CollectionExistsAsync(collectionName, cancellationToken);
            if (!exists) return (0, false);

            CollectionInfo info = await GetClient().GetCollectionInfoAsync(collectionName, cancellationToken);
            return (info.PointsCount, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get info for collection {Collection}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Delete a collection.
    /// </summary>
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            bool exists = await GetClient().CollectionExistsAsync(collectionName, cancellationToken);
            if (exists)
            {
                await GetClient().DeleteCollectionAsync(collectionName, cancellationToken: cancellationToken);
                _logger.LogInformation("Deleted collection {Collection}", collectionName);
            }

            NotifyCollectionChanged(collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete collection {Collection}", collectionName);
            throw;
        }
    }

    /// <summary>
    /// List all collections.
    /// </summary>
    public async Task<List<string>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<string> collections = await GetClient().ListCollectionsAsync(cancellationToken);
            return collections.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list collections");
            throw;
        }
    }

    /// <summary>
    /// Check if a collection exists.
    /// </summary>
    public async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetClient().CollectionExistsAsync(collectionName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if collection {Collection} exists", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Create a payload index on a field for efficient filtering.
    /// Idempotent — no-op if the index already exists.
    /// </summary>
    public async Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetClient().CreatePayloadIndexAsync(
                collectionName,
                fieldName,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Created payload index on {Field} in {Collection}", fieldName, collectionName);
        }
        catch (Exception ex)
        {
            // EnsurePayloadIndexesAsync only calls this for fields Qdrant reports as missing, so an
            // error here is not an ignorable "may already exist" condition. Filtering and exact
            // deletes depend on these indexes and indexing must not report success without them.
            _logger.LogError(ex, "Failed to create required payload index on {Field} in {Collection}",
                fieldName, collectionName);
            throw;
        }
    }

    /// <summary>
    /// Search for chunks whose symbol_name matches any of the given names.
    /// Used to find callee definitions from calls_out edges.
    /// </summary>
    public async Task<List<SearchResult>> SearchBySymbolNamesAsync(
        string collectionName,
        IReadOnlyList<string> symbolNames,
        CancellationToken cancellationToken = default)
    {
        if (symbolNames.Count == 0) return [];

        try
        {
            var conditions = symbolNames.SelectMany(name => new[]
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "symbol_name",
                        Match = new Match { Keyword = name }
                    }
                },
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "canonical_symbol_name",
                        Match = new Match { Keyword = name }
                    }
                }
            }).ToList();

            var filter = new Filter();
            filter.Should.AddRange(conditions);

            uint resultLimit = (uint)Math.Min(Math.Max(symbolNames.Count * 10, 100), 500);
            ScrollResponse response = await GetClient().ScrollAsync(
                collectionName,
                filter: filter,
                limit: resultLimit,
                cancellationToken: cancellationToken);

            var requestedNames = symbolNames.ToHashSet(StringComparer.Ordinal);
            List<SearchResult> exactResults = response.Result
                .Select(r => new SearchResult
                {
                    Score = 0f, // Not vector-scored
                    Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
                })
                .ToList();

            // Legacy fallback for split chunks indexed before canonical_symbol_name existed.
            // Keep this separate from the primary query: parent_symbol also identifies every
            // ordinary member's containing type and would otherwise flood class-name lookups.
            var legacyFilter = new Filter();
            legacyFilter.Should.AddRange(symbolNames.Select(name => new Condition
            {
                Field = new FieldCondition
                {
                    Key = "parent_symbol",
                    Match = new Match { Keyword = name }
                }
            }));
            ScrollResponse legacyResponse = await GetClient().ScrollAsync(
                collectionName,
                filter: legacyFilter,
                limit: 500,
                cancellationToken: cancellationToken);

            List<SearchResult> legacyResults = legacyResponse.Result
                .Select(r => new SearchResult
                {
                    Score = 0f,
                    Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
                })
                .ToList();

            return MergeRequestedSymbolMatches(exactResults, legacyResults, requestedNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search by symbol names in {Collection}", collectionName);
            throw;
        }
    }

    public async Task<List<SearchResult>> SearchByQualifiedNamesAsync(
        string collectionName,
        IReadOnlyList<string> qualifiedNames,
        CancellationToken cancellationToken = default)
    {
        if (qualifiedNames.Count == 0) return [];

        var conditions = qualifiedNames.SelectMany(name => new[]
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "qualified_name",
                    Match = new Match { Keyword = name }
                }
            },
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "canonical_qualified_name",
                    Match = new Match { Keyword = name }
                }
            }
        }).ToList();
        var filter = new Filter();
        filter.Should.AddRange(conditions);

        ScrollResponse response = await GetClient().ScrollAsync(
            collectionName,
            filter: filter,
            // A single logical method can be split into considerably more than three indexed
            // fragments. Fetch the complete bounded match set so dependency consolidation does
            // not start halfway through a large method merely because Qdrant returned later parts
            // first. The caller already caps the consolidated dependency list.
            limit: 100,
            cancellationToken: cancellationToken);

        var requestedNames = qualifiedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<SearchResult> exactResults = response.Result.Select(result => new SearchResult
        {
            Score = 0f,
            Chunk = BuildChunkFromPayload(result.Id.Uuid, result.Payload)
        }).Where(result => MatchesRequestedQualifiedName(result.Chunk, requestedNames)).ToList();

        HashSet<string> matchedNames = exactResults
            .Select(result => GetCanonicalQualifiedName(result.Chunk))
            .Where(name => name != null)
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> missingNames = qualifiedNames
            .Where(name => !matchedNames.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missingNames.Count == 0)
            return exactResults;

        // Legacy split chunks have a suffixed qualified_name and no canonical field. Resolve their
        // base symbols through the legacy-aware symbol path, then verify the full canonical name so
        // same-named members on unrelated types cannot leak into dependency expansion.
        List<string> fallbackSymbols = missingNames
            .Select(name => name[(name.LastIndexOf('.') + 1)..])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        List<SearchResult> legacyCandidates = await SearchBySymbolNamesAsync(
            collectionName, fallbackSymbols, cancellationToken);

        return exactResults
            .Concat(legacyCandidates.Where(result =>
                MatchesRequestedQualifiedName(result.Chunk, requestedNames)))
            .DistinctBy(result => result.Chunk.Id)
            .ToList();
    }

    /// <summary>
    /// Search for chunks whose calls_out contains the given symbol name.
    /// Used to find callers of a given symbol.
    /// </summary>
    public async Task<List<SearchResult>> SearchCallersOfAsync(
        string collectionName,
        string symbolName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "calls_out_names",
                            Match = new Match { Keyword = symbolName }
                        }
                    }
                }
            };

            ScrollResponse response = await GetClient().ScrollAsync(
                collectionName,
                filter: filter,
                limit: 50,
                cancellationToken: cancellationToken);

            return response.Result.Select(r => new SearchResult
            {
                Score = 0f,
                Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search callers of {Symbol} in {Collection}",
                symbolName, collectionName);
            throw;
        }
    }

    private static CodeChunk BuildChunkFromPayload(string uuid, MapField<string, Value> payload)
    {
        return new CodeChunk
        {
            Id = Guid.Parse(uuid),
            FilePath = payload["file_path"].StringValue,
            RelativePath = payload["relative_path"].StringValue,
            Content = payload["content"].StringValue,
            StartLine = (int)payload["start_line"].IntegerValue,
            EndLine = (int)payload["end_line"].IntegerValue,
            ChunkType = payload["chunk_type"].StringValue,
            SymbolName = GetOptionalString(payload, "symbol_name"),
            ParentSymbol = GetOptionalString(payload, "parent_symbol"),
            Language = payload["language"].StringValue,
            ContentHash = payload["content_hash"].StringValue,
            CallsOut = ParseCallsOut(payload),
            ReturnType = GetOptionalString(payload, "return_type"),
            BaseType = GetOptionalString(payload, "base_type"),
            ImplementedInterfaces = ParseStringList(payload, "implemented_interfaces"),
            AccessModifier = GetOptionalString(payload, "access_modifier"),
            Modifiers = ParseStringList(payload, "modifiers"),
            Attributes = ParseStringList(payload, "attributes"),
            Namespace = GetOptionalString(payload, "namespace"),
            QualifiedName = GetOptionalString(payload, "qualified_name"),
            Parameters = ParseParameters(payload),
            FieldAccesses = ParseFieldAccesses(payload)
        };
    }

    private static IReadOnlyList<CallReference>? ParseCallsOut(MapField<string, Value> payload)
    {
        if (!payload.TryGetValue("calls_out", out Value? value))
            return null;

        if (value.KindCase != Value.KindOneofCase.ListValue)
            return null;

        var calls = new List<CallReference>();
        foreach (Value item in value.ListValue.Values)
        {
            if (item.KindCase == Value.KindOneofCase.StructValue)
            {
                MapField<string, Value> fields = item.StructValue.Fields;
                string methodName = fields.TryGetValue("method_name", out Value? mn) ? mn.StringValue : "";
                if (string.IsNullOrEmpty(methodName)) continue;

                calls.Add(new CallReference
                {
                    MethodName = methodName,
                    ReceiverType = fields.TryGetValue("receiver_type", out Value? rt) && !string.IsNullOrEmpty(rt.StringValue) ? rt.StringValue : null,
                    ReceiverExpression = fields.TryGetValue("receiver_expression", out Value? re) && !string.IsNullOrEmpty(re.StringValue) ? re.StringValue : null,
                    QualifiedName = fields.TryGetValue("qualified_name", out Value? qn) && !string.IsNullOrEmpty(qn.StringValue) ? qn.StringValue : null,
                    Line = fields.TryGetValue("line", out Value? ln) ? (int)ln.IntegerValue : 0
                });
            }
            else if (item.KindCase == Value.KindOneofCase.StringValue && !string.IsNullOrEmpty(item.StringValue))
            {
                // Backward compat: old data stored as bare strings
                calls.Add(new CallReference { MethodName = item.StringValue });
            }
        }

        return calls.Count > 0 ? calls : null;
    }

    internal static ListValue BuildCallReferenceList(IReadOnlyList<CallReference> calls)
    {
        var list = new ListValue();
        foreach (CallReference call in calls)
        {
            var fields = new Struct();
            fields.Fields["method_name"] = new Value { StringValue = call.MethodName };
            fields.Fields["line"] = new Value { IntegerValue = call.Line };
            if (call.ReceiverType != null)
                fields.Fields["receiver_type"] = new Value { StringValue = call.ReceiverType };
            if (call.ReceiverExpression != null)
                fields.Fields["receiver_expression"] = new Value { StringValue = call.ReceiverExpression };
            if (call.QualifiedName != null)
                fields.Fields["qualified_name"] = new Value { StringValue = call.QualifiedName };

            list.Values.Add(new Value { StructValue = fields });
        }
        return list;
    }

    internal static ListValue BuildStringList(IReadOnlyList<string> values)
    {
        var list = new ListValue();
        foreach (string val in values)
        {
            list.Values.Add(new Value { StringValue = val });
        }
        return list;
    }

    internal static ListValue BuildParameterList(IReadOnlyList<ParameterInfo> parameters)
    {
        var list = new ListValue();
        foreach (ParameterInfo p in parameters)
        {
            var fields = new Struct();
            fields.Fields["name"] = new Value { StringValue = p.Name };
            if (p.Type != null) fields.Fields["type"] = new Value { StringValue = p.Type };
            if (p.DefaultValue != null) fields.Fields["default_value"] = new Value { StringValue = p.DefaultValue };
            if (p.IsOut) fields.Fields["is_out"] = new Value { BoolValue = true };
            if (p.IsRef) fields.Fields["is_ref"] = new Value { BoolValue = true };
            if (p.IsParams) fields.Fields["is_params"] = new Value { BoolValue = true };
            list.Values.Add(new Value { StructValue = fields });
        }
        return list;
    }

    internal static ListValue BuildFieldAccessList(IReadOnlyList<FieldAccess> accesses)
    {
        var list = new ListValue();
        foreach (FieldAccess fa in accesses)
        {
            var fields = new Struct();
            fields.Fields["field_name"] = new Value { StringValue = fa.FieldName };
            if (fa.ContainingType != null) fields.Fields["containing_type"] = new Value { StringValue = fa.ContainingType };
            fields.Fields["kind"] = new Value { StringValue = fa.Kind.ToString() };
            fields.Fields["line"] = new Value { IntegerValue = fa.Line };
            list.Values.Add(new Value { StructValue = fields });
        }
        return list;
    }

    private static string? GetOptionalString(MapField<string, Value> payload, string key)
    {
        if (!payload.TryGetValue(key, out Value? value)) return null;
        return string.IsNullOrEmpty(value.StringValue) ? null : value.StringValue;
    }

    private static IReadOnlyList<string>? ParseStringList(MapField<string, Value> payload, string key)
    {
        if (!payload.TryGetValue(key, out Value? value)) return null;
        if (value.KindCase != Value.KindOneofCase.ListValue) return null;

        List<string> items = value.ListValue.Values
            .Where(v => v.KindCase == Value.KindOneofCase.StringValue && !string.IsNullOrEmpty(v.StringValue))
            .Select(v => v.StringValue)
            .ToList();

        return items.Count > 0 ? items : null;
    }

    private static IReadOnlyList<ParameterInfo>? ParseParameters(MapField<string, Value> payload)
    {
        if (!payload.TryGetValue("parameters", out Value? value)) return null;
        if (value.KindCase != Value.KindOneofCase.ListValue) return null;

        var parameters = new List<ParameterInfo>();
        foreach (Value item in value.ListValue.Values)
        {
            if (item.KindCase != Value.KindOneofCase.StructValue) continue;
            MapField<string, Value> f = item.StructValue.Fields;

            string name = f.TryGetValue("name", out Value? n) ? n.StringValue : "";
            if (string.IsNullOrEmpty(name)) continue;

            parameters.Add(new ParameterInfo
            {
                Name = name,
                Type = f.TryGetValue("type", out Value? t) && !string.IsNullOrEmpty(t.StringValue) ? t.StringValue : null,
                DefaultValue = f.TryGetValue("default_value", out Value? dv) && !string.IsNullOrEmpty(dv.StringValue) ? dv.StringValue : null,
                IsOut = f.TryGetValue("is_out", out Value? io) && io.BoolValue,
                IsRef = f.TryGetValue("is_ref", out Value? ir) && ir.BoolValue,
                IsParams = f.TryGetValue("is_params", out Value? ip) && ip.BoolValue
            });
        }

        return parameters.Count > 0 ? parameters : null;
    }

    private static IReadOnlyList<FieldAccess>? ParseFieldAccesses(MapField<string, Value> payload)
    {
        if (!payload.TryGetValue("field_accesses", out Value? value)) return null;
        if (value.KindCase != Value.KindOneofCase.ListValue) return null;

        var accesses = new List<FieldAccess>();
        foreach (Value item in value.ListValue.Values)
        {
            if (item.KindCase != Value.KindOneofCase.StructValue) continue;
            MapField<string, Value> f = item.StructValue.Fields;

            string fieldName = f.TryGetValue("field_name", out Value? fn) ? fn.StringValue : "";
            if (string.IsNullOrEmpty(fieldName)) continue;

            accesses.Add(new FieldAccess
            {
                FieldName = fieldName,
                ContainingType = f.TryGetValue("containing_type", out Value? ct) && !string.IsNullOrEmpty(ct.StringValue) ? ct.StringValue : null,
                Kind = f.TryGetValue("kind", out Value? k) && Enum.TryParse<FieldAccessKind>(k.StringValue, out var kind) ? kind : FieldAccessKind.Read,
                Line = f.TryGetValue("line", out Value? ln) ? (int)ln.IntegerValue : 0
            });
        }

        return accesses.Count > 0 ? accesses : null;
    }

    // ────────────────────────────────────────────────────────────────
    //  Scroll all chunks (used by graph builder)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scroll through all chunks in a collection, paginating automatically.
    /// Used to build the in-memory data flow graph.
    /// </summary>
    public async Task<List<CodeChunk>> ScrollAllChunksAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        var allChunks = new List<CodeChunk>();

        try
        {
            PointId? offset = null;

            while (true)
            {
                ScrollResponse response = await GetClient().ScrollAsync(
                    collectionName,
                    limit: 250,
                    offset: offset,
                    cancellationToken: cancellationToken);

                foreach (RetrievedPoint point in response.Result)
                {
                    allChunks.Add(BuildChunkFromPayload(point.Id.Uuid, point.Payload));
                }

                if (response.NextPageOffset == null)
                    break;

                offset = response.NextPageOffset;
            }

            _logger.LogDebug("Scrolled {Count} chunks from {Collection}", allChunks.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scroll all chunks from {Collection}", collectionName);
            throw;
        }

        return allChunks;
    }

    // ────────────────────────────────────────────────────────────────
    //  4b — Payload Indexes
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Create payload indexes on all enriched fields for efficient graph queries.
    /// Idempotent — safe to call on existing collections.
    /// </summary>
    public static IReadOnlyList<string> RequiredPayloadIndexFields { get; } =
    [
        "qualified_name",
        "canonical_qualified_name",
        "base_type",
        "implemented_interfaces",
        "namespace",
        "return_type",
        "access_modifier",
        "calls_out_names",
        "symbol_name",
        "canonical_symbol_name",
        "parent_symbol",
        "chunk_type",
        // Without this, every delete and every per-file scroll full-scans the collection.
        "relative_path"
    ];

    public async Task EnsurePayloadIndexesAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        List<string> missingFields = await GetMissingPayloadIndexesAsync(collectionName, cancellationToken);
        foreach (string field in missingFields)
        {
            await CreatePayloadIndexAsync(collectionName, field, cancellationToken);
        }

        List<string> stillMissing = await GetMissingPayloadIndexesAsync(collectionName, cancellationToken);
        if (stillMissing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Collection '{collectionName}' is missing required payload indexes: "
                + string.Join(", ", stillMissing));
        }
    }

    public async Task<List<string>> GetMissingPayloadIndexesAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        bool exists = await GetClient().CollectionExistsAsync(collectionName, cancellationToken);
        if (!exists) return RequiredPayloadIndexFields.ToList();

        CollectionInfo info = await GetClient().GetCollectionInfoAsync(collectionName, cancellationToken);
        return FindMissingPayloadIndexes(info.PayloadSchema.Keys);
    }

    internal static List<string> FindMissingPayloadIndexes(IEnumerable<string> indexedFields)
    {
        HashSet<string> existing = indexedFields.ToHashSet(StringComparer.Ordinal);
        return RequiredPayloadIndexFields.Where(field => !existing.Contains(field)).ToList();
    }

    internal static bool MatchesRequestedSymbol(CodeChunk chunk, IReadOnlySet<string> requestedNames)
    {
        if (chunk.SymbolName is not { Length: > 0 } symbolName)
            return false;

        string canonicalName = SearchResultDiversifier.RemovePartSuffix(symbolName);
        if (requestedNames.Contains(canonicalName))
            return true;

        return SearchResultDiversifier.BaseChunkType(chunk.ChunkType) != chunk.ChunkType
            && chunk.ParentSymbol is { Length: > 0 } parentSymbol
            && requestedNames.Contains(parentSymbol);
    }

    internal static List<SearchResult> MergeRequestedSymbolMatches(
        IEnumerable<SearchResult> exactResults,
        IEnumerable<SearchResult> legacyResults,
        IReadOnlySet<string> requestedNames)
    {
        return exactResults
            .Concat(legacyResults)
            .Where(result => MatchesRequestedSymbol(result.Chunk, requestedNames))
            .DistinctBy(result => result.Chunk.Id)
            .ToList();
    }

    internal static bool MatchesRequestedQualifiedName(
        CodeChunk chunk,
        IReadOnlySet<string> requestedNames)
    {
        string? canonicalName = GetCanonicalQualifiedName(chunk);
        return canonicalName != null && requestedNames.Contains(canonicalName);
    }

    private static string? GetCanonicalQualifiedName(CodeChunk chunk) =>
        chunk.QualifiedName is { Length: > 0 } qualifiedName
            ? SearchResultDiversifier.RemovePartSuffix(qualifiedName)
            : null;

    // ────────────────────────────────────────────────────────────────
    //  4c — Graph Query Methods
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Find all chunks that implement a given interface.
    /// </summary>
    public async Task<List<SearchResult>> FindImplementationsOfAsync(
        string collectionName,
        string interfaceName,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "implemented_interfaces", interfaceName, cancellationToken,
            maxResults: maxResults ?? DefaultGraphQueryLimit);
    }

    /// <summary>
    /// Find all chunks that extend a given base type.
    /// </summary>
    public async Task<List<SearchResult>> FindSubclassesOfAsync(
        string collectionName,
        string baseType,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "base_type", baseType, cancellationToken,
            maxResults: maxResults ?? DefaultGraphQueryLimit);
    }

    /// <summary>
    /// Find methods returning a specific type.
    /// </summary>
    public async Task<List<SearchResult>> FindMethodsByReturnTypeAsync(
        string collectionName,
        string typeName,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "return_type", typeName, cancellationToken,
            maxResults: maxResults ?? DefaultGraphQueryLimit);
    }

    /// <summary>
    /// Find a chunk by its fully qualified name.
    /// </summary>
    public async Task<SearchResult?> FindByQualifiedNameAsync(
        string collectionName,
        string qualifiedName,
        CancellationToken cancellationToken = default)
    {
        List<SearchResult> results = await ScrollWithKeywordFilterAsync(
            collectionName, "qualified_name", qualifiedName, cancellationToken, pageSize: 1, maxResults: 1);
        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>
    /// Find all chunks in a given namespace.
    /// </summary>
    public async Task<List<SearchResult>> FindByNamespaceAsync(
        string collectionName,
        string namespaceName,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "namespace", namespaceName, cancellationToken,
            maxResults: maxResults ?? DefaultGraphQueryLimit);
    }

    /// <summary>
    /// Trace a call chain forward or backward from a symbol, up to the given depth.
    /// Returns all chunks in the chain, grouped by hop distance.
    /// </summary>
    public async Task<Dictionary<int, List<SearchResult>>> TraceCallChainAsync(
        string collectionName,
        string symbolName,
        int maxDepth = 3,
        bool forward = true,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, List<SearchResult>>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentSymbols = new List<string> { symbolName };

        for (int depth = 1; depth <= maxDepth && currentSymbols.Count > 0; depth++)
        {
            var hitsAtDepth = new List<SearchResult>();
            var nextSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (forward)
            {
                // Forward: find definitions of the symbols we call
                List<SearchResult> definitions = await SearchBySymbolNamesAsync(
                    collectionName, currentSymbols, cancellationToken);

                foreach (SearchResult def in definitions)
                {
                    string key = def.Chunk.QualifiedName ?? def.Chunk.SymbolName ?? "";
                    if (string.IsNullOrEmpty(key) || !visited.Add(key)) continue;

                    hitsAtDepth.Add(def);

                    if (def.Chunk.CallsOut is { Count: > 0 })
                    {
                        foreach (CallReference call in def.Chunk.CallsOut)
                            nextSymbols.Add(call.MethodName);
                    }
                }
            }
            else
            {
                // Backward: find callers of the current symbols
                foreach (string sym in currentSymbols)
                {
                    List<SearchResult> callers = await SearchCallersOfAsync(
                        collectionName, sym, cancellationToken);

                    foreach (SearchResult caller in callers)
                    {
                        string key = caller.Chunk.QualifiedName ?? caller.Chunk.SymbolName ?? "";
                        if (string.IsNullOrEmpty(key) || !visited.Add(key)) continue;

                        hitsAtDepth.Add(caller);
                        if (!string.IsNullOrEmpty(caller.Chunk.SymbolName))
                            nextSymbols.Add(caller.Chunk.SymbolName);
                    }
                }
            }

            if (hitsAtDepth.Count > 0)
                result[depth] = hitsAtDepth;

            currentSymbols = nextSymbols.ToList();
        }

        return result;
    }

    private async Task<List<SearchResult>> ScrollWithKeywordFilterAsync(
        string collectionName,
        string fieldKey,
        string value,
        CancellationToken cancellationToken,
        uint pageSize = 100,
        int? maxResults = null)
    {
        var results = new List<SearchResult>();

        try
        {
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = fieldKey,
                            Match = new Match { Keyword = value }
                        }
                    }
                }
            };

            PointId? offset = null;

            // A single scroll returns at most `pageSize` points. Left unpaged this silently truncated
            // every file over 100 chunks — real files in these collections run to 368 — so the graph
            // was rebuilt from a partial view with no error anywhere.
            while (true)
            {
                ScrollResponse response = await GetClient().ScrollAsync(
                    collectionName,
                    filter: filter,
                    limit: pageSize,
                    offset: offset,
                    cancellationToken: cancellationToken);

                results.AddRange(response.Result.Select(r => new SearchResult
                {
                    Score = 0f,
                    Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
                }));

                // pageSize is how many points come back per round trip; maxResults is how many the
                // caller actually wants. Conflating the two turned a unique-key lookup into a scan of
                // every matching point, one round trip at a time.
                if (maxResults is not null && results.Count >= maxResults.Value)
                {
                    if (response.NextPageOffset is not null)
                    {
                        _logger.LogWarning(
                            "Reached the {Max}-result cap for {Field}={Value} in {Collection}; more "
                            + "matches exist and are not being returned",
                            maxResults.Value, fieldKey, value, collectionName);
                    }

                    return results.Take(maxResults.Value).ToList();
                }

                // Qdrant signals the last page with a null offset. Guard the contract violation too:
                // an offset that does not advance would otherwise spin here forever, hanging the
                // caller rather than failing it.
                if (response.NextPageOffset is null) break;
                if (offset is not null && response.NextPageOffset.Equals(offset))
                {
                    _logger.LogWarning(
                        "Scroll offset did not advance for {Field}={Value} in {Collection}; stopping "
                        + "after {Count} results to avoid looping.", fieldKey, value, collectionName, results.Count);
                    break;
                }

                offset = response.NextPageOffset;
            }

            return results;
        }
        catch (Exception ex)
        {
            // Deliberately rethrown rather than returning what was collected. A partial page set is
            // indistinguishable from a complete one at the call site, so a transient failure on page
            // four of ten would quietly become "this file has 300 chunks" — the same silent-truncation
            // failure the paging loop above exists to remove. An empty result must mean "no chunks".
            _logger.LogError(ex,
                "Failed to scroll {Field}={Value} in {Collection} after {Count} result(s); "
                + "discarding the partial set rather than returning it as complete",
                fieldKey, value, collectionName, results.Count);
            throw;
        }
    }

    /// <summary>
    /// Upsert pre-computed vectors with payloads (used by L2 promotion).
    /// No embedding computation needed - vectors are already computed.
    /// </summary>
    public async Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points,
        CancellationToken cancellationToken = default)
    {
        if (points.Count == 0) return;

        try
        {
            List<PointStruct> qdrantPoints = points.Select(p =>
            {
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = p.id.ToString() },
                    Vectors = p.vector
                };

                foreach ((string key, object value) in p.payload)
                {
                    point.Payload[key] = value switch
                    {
                        Value v => v,
                        string s => s,
                        int i => i,
                        long l => l,
                        float f => f,
                        double d => d,
                        bool b => b,
                        DateTime dt => dt.ToString("O"),
                        IReadOnlyList<string> list => new Value { ListValue = BuildStringList(list) },
                        _ => value.ToString() ?? ""
                    };
                }

                return point;
            }).ToList();

            await GetClient().UpsertAsync(collectionName, qdrantPoints, cancellationToken: cancellationToken);
            NotifyCollectionChanged(collectionName);

            _logger.LogDebug("Upserted {Count} pre-computed points to collection {Collection}",
                points.Count, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert {Count} points to collection {Collection}",
                points.Count, collectionName);
            throw;
        }
    }

    private void NotifyCollectionChanged(string collectionName)
    {
        try
        {
            CollectionChanged?.Invoke(collectionName);
        }
        catch (Exception ex)
        {
            // Cache invalidation must never turn a completed Qdrant write into a reported write failure.
            _logger.LogError(ex, "A collection-change observer failed for {Collection}", collectionName);
        }
    }
}
