using CodeAssist.Core.Configuration;
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
public sealed class QdrantService
{
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

                await EnsurePayloadIndexesAsync(collectionName, cancellationToken);
            }
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
                    ["parameters"] = chunk.Parameters is { Count: > 0 }
                        ? new Value { ListValue = BuildParameterList(chunk.Parameters) }
                        : new Value { ListValue = new ListValue() },
                    ["field_accesses"] = chunk.FieldAccesses is { Count: > 0 }
                        ? new Value { ListValue = BuildFieldAccessList(chunk.FieldAccesses) }
                        : new Value { ListValue = new ListValue() }
                }
            }).ToList();

            await GetClient().UpsertAsync(collectionName, points, cancellationToken: cancellationToken);

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
                filter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "relative_path",
                                Match = new Match { Text = filePathFilter }
                            }
                        }
                    }
                };
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
    /// Delete chunks by file path.
    /// </summary>
    public async Task DeleteByFilePathAsync(
        string collectionName,
        string relativePath,
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
                            Key = "relative_path",
                            Match = new Match { Text = relativePath }
                        }
                    }
                }
            };

            await GetClient().DeleteAsync(collectionName, filter, cancellationToken: cancellationToken);

            _logger.LogDebug("Deleted chunks for file {FilePath} from collection {Collection}", relativePath, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunks for file {FilePath} from collection {Collection}", relativePath, collectionName);
            throw;
        }
    }

    /// <summary>
    /// Scroll all chunks for a given file path (used by graph rebuild).
    /// </summary>
    public async Task<List<SearchResult>> SearchByFilePathAsync(
        string collectionName,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "relative_path", relativePath, cancellationToken);
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
            // Non-fatal — index may already exist or field type mismatch
            _logger.LogWarning(ex, "Failed to create payload index on {Field} in {Collection} (may already exist)",
                fieldName, collectionName);
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
            // Build OR filter: symbol_name matches any of the given names
            var conditions = symbolNames.Select(name => new Condition
            {
                Field = new FieldCondition
                {
                    Key = "symbol_name",
                    Match = new Match { Keyword = name }
                }
            }).ToList();

            var filter = new Filter();
            filter.Should.AddRange(conditions);

            ScrollResponse response = await GetClient().ScrollAsync(
                collectionName,
                filter: filter,
                limit: (uint)Math.Min(symbolNames.Count * 3, 100),
                cancellationToken: cancellationToken);

            return response.Result.Select(r => new SearchResult
            {
                Score = 0f, // Not vector-scored
                Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search by symbol names in {Collection}", collectionName);
            return [];
        }
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
            return [];
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
    public async Task EnsurePayloadIndexesAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        string[] indexFields =
        [
            "qualified_name",
            "base_type",
            "implemented_interfaces",
            "namespace",
            "return_type",
            "access_modifier",
            "calls_out_names",
            "symbol_name",
            "chunk_type"
        ];

        foreach (string field in indexFields)
        {
            await CreatePayloadIndexAsync(collectionName, field, cancellationToken);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  4c — Graph Query Methods
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Find all chunks that implement a given interface.
    /// </summary>
    public async Task<List<SearchResult>> FindImplementationsOfAsync(
        string collectionName,
        string interfaceName,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "implemented_interfaces", interfaceName, cancellationToken);
    }

    /// <summary>
    /// Find all chunks that extend a given base type.
    /// </summary>
    public async Task<List<SearchResult>> FindSubclassesOfAsync(
        string collectionName,
        string baseType,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "base_type", baseType, cancellationToken);
    }

    /// <summary>
    /// Find methods returning a specific type.
    /// </summary>
    public async Task<List<SearchResult>> FindMethodsByReturnTypeAsync(
        string collectionName,
        string typeName,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "return_type", typeName, cancellationToken);
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
            collectionName, "qualified_name", qualifiedName, cancellationToken, limit: 1);
        return results.Count > 0 ? results[0] : null;
    }

    /// <summary>
    /// Find all chunks in a given namespace.
    /// </summary>
    public async Task<List<SearchResult>> FindByNamespaceAsync(
        string collectionName,
        string namespaceName,
        CancellationToken cancellationToken = default)
    {
        return await ScrollWithKeywordFilterAsync(
            collectionName, "namespace", namespaceName, cancellationToken);
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
        uint limit = 100)
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
                            Key = fieldKey,
                            Match = new Match { Keyword = value }
                        }
                    }
                }
            };

            ScrollResponse response = await GetClient().ScrollAsync(
                collectionName,
                filter: filter,
                limit: limit,
                cancellationToken: cancellationToken);

            return response.Result.Select(r => new SearchResult
            {
                Score = 0f,
                Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scroll {Field}={Value} in {Collection}",
                fieldKey, value, collectionName);
            return [];
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
}
