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
/// </summary>
public sealed class QdrantService
{
    private readonly QdrantClient _client;
    private readonly CodeAssistOptions _options;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(
        IOptions<CodeAssistOptions> options,
        ILogger<QdrantService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var uri = new Uri(_options.QdrantUrl);

        // Use port 6334 for gRPC (Qdrant's dedicated gRPC port)
        // Port 6333 is REST API, port 6334 is gRPC
        int grpcPort = uri.Port == 6333 ? 6334 : uri.Port;

        _client = new QdrantClient(
            host: uri.Host,
            port: grpcPort,
            https: uri.Scheme == "https");
    }

    /// <summary>
    /// Ensure a collection exists with the correct configuration.
    /// </summary>
    public async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            bool exists = await _client.CollectionExistsAsync(collectionName, cancellationToken);

            if (!exists)
            {
                _logger.LogInformation("Creating collection {Collection}", collectionName);

                await _client.CreateCollectionAsync(
                    collectionName,
                    new VectorParams
                    {
                        Size = (ulong)_options.VectorDimension,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);
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
                        ? new Value { ListValue = BuildStringList(chunk.CallsOut) }
                        : new Value { ListValue = new ListValue() }
                }
            }).ToList();

            await _client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);

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

            IReadOnlyList<ScoredPoint> results = await _client.SearchAsync(
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

            await _client.DeleteAsync(collectionName, filter, cancellationToken: cancellationToken);

            _logger.LogDebug("Deleted chunks for file {FilePath} from collection {Collection}", relativePath, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunks for file {FilePath} from collection {Collection}", relativePath, collectionName);
            throw;
        }
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
            await _client.DeleteAsync(collectionName, pointIds, cancellationToken: cancellationToken);

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
            bool exists = await _client.CollectionExistsAsync(collectionName, cancellationToken);
            if (!exists) return (0, false);

            CollectionInfo info = await _client.GetCollectionInfoAsync(collectionName, cancellationToken);
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
            bool exists = await _client.CollectionExistsAsync(collectionName, cancellationToken);
            if (exists)
            {
                await _client.DeleteCollectionAsync(collectionName, cancellationToken: cancellationToken);
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
            IReadOnlyList<string> collections = await _client.ListCollectionsAsync(cancellationToken);
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
            return await _client.CollectionExistsAsync(collectionName, cancellationToken);
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
            await _client.CreatePayloadIndexAsync(
                collectionName,
                fieldName,
                PayloadSchemaType.Keyword,
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

            ScrollResponse response = await _client.ScrollAsync(
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
                            Key = "calls_out",
                            Match = new Match { Keyword = symbolName }
                        }
                    }
                }
            };

            ScrollResponse response = await _client.ScrollAsync(
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
            SymbolName = string.IsNullOrEmpty(payload["symbol_name"].StringValue)
                ? null
                : payload["symbol_name"].StringValue,
            ParentSymbol = string.IsNullOrEmpty(payload["parent_symbol"].StringValue)
                ? null
                : payload["parent_symbol"].StringValue,
            Language = payload["language"].StringValue,
            ContentHash = payload["content_hash"].StringValue,
            CallsOut = ParseCallsOut(payload)
        };
    }

    private static IReadOnlyList<string>? ParseCallsOut(MapField<string, Value> payload)
    {
        if (!payload.TryGetValue("calls_out", out Value? value))
            return null;

        if (value.KindCase != Value.KindOneofCase.ListValue)
            return null;

        List<string> calls = value.ListValue.Values
            .Where(v => v.KindCase == Value.KindOneofCase.StringValue && !string.IsNullOrEmpty(v.StringValue))
            .Select(v => v.StringValue)
            .ToList();

        return calls.Count > 0 ? calls : null;
    }

    private static ListValue BuildStringList(IReadOnlyList<string> values)
    {
        var list = new ListValue();
        foreach (string val in values)
        {
            list.Values.Add(new Value { StringValue = val });
        }
        return list;
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

            await _client.UpsertAsync(collectionName, qdrantPoints, cancellationToken: cancellationToken);

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
