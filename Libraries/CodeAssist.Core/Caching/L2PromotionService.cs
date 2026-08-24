using System.Collections.Concurrent;
using System.Threading.Channels;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;

namespace CodeAssist.Core.Caching;

/// <summary>
/// Background service that promotes L1 cache entries to L2 (Qdrant).
/// Pre-computed embeddings from L1 are transferred without re-embedding.
/// </summary>
public sealed class L2PromotionService : IDisposable
{
    private readonly Channel<PromotionTask> _promotionQueue;
    private readonly IQdrantWriter _qdrantService;
    private readonly IndexStateStore _indexStateStore;
    private readonly CodeAssistOptions _options;
    private readonly ILogger<L2PromotionService> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _processingTask;
    private readonly ConcurrentDictionary<string, string> _fileToCollection = new(); // filePath -> collectionName
    private bool _disposed;

    public L2PromotionService(
        HotCache hotCache,
        IQdrantWriter qdrantService,
        IndexStateStore indexStateStore,
        IOptions<CodeAssistOptions> options,
        ILogger<L2PromotionService> logger)
    {
        _qdrantService = qdrantService;
        _indexStateStore = indexStateStore;
        _options = options.Value;
        _logger = logger;

        // Bounded channel to prevent memory issues
        _promotionQueue = Channel.CreateBounded<PromotionTask>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        // Subscribe to hot cache promotion events
        hotCache.FileReadyForPromotion += OnFileReadyForPromotion;

        // Start background processing
        _processingTask = Task.Run(ProcessPromotionQueueAsync);

        _logger.LogInformation("L2PromotionService started");
    }

    /// <summary>
    /// Number of pending promotion tasks.
    /// </summary>
    public int PendingCount => _promotionQueue.Reader.Count;

    /// <summary>
    /// Files whose changes were cached in L1 but never reached Qdrant, because no collection could be
    /// resolved for them or the resolved collection did not exist. Surfaced through
    /// <c>get_watched_repositories</c> so this failure is a number a caller can see rather than a log
    /// line nobody reads: a dropped promotion means an edit that disappears when the process exits,
    /// leaving the index quietly stale with no error anywhere.
    /// </summary>
    public int DroppedPromotionCount => Volatile.Read(ref _droppedPromotions);

    private int _droppedPromotions;

    /// <summary>
    /// Register all files in a repository with a collection.
    /// </summary>
    public void RegisterRepositoryCollection(string repositoryRoot, string collectionName)
    {
        // This will be used when files are promoted - we store the mapping
        _fileToCollection[Path.GetFullPath(repositoryRoot)] = collectionName;
    }

    /// <summary>
    /// Queue a file for L2 promotion manually.
    /// </summary>
    public async Task QueuePromotionAsync(CachedFile cachedFile, string collectionName)
    {
        if (!_options.EnableL2Promotion)
        {
            _logger.LogDebug("L2 promotion disabled, skipping");
            return;
        }

        var task = new PromotionTask
        {
            CachedFile = cachedFile,
            CollectionName = collectionName,
            QueuedAt = DateTime.UtcNow
        };

        await _promotionQueue.Writer.WriteAsync(task);
        _logger.LogDebug("Queued {File} for L2 promotion to {Collection}",
            cachedFile.RelativePath, collectionName);
    }

    /// <summary>
    /// Promote a file immediately rather than through the background queue.
    /// </summary>
    /// <remarks>
    /// A test seam, not API: the queue's timing makes the delete-then-upsert ordering impossible to
    /// observe reliably. Internal because <see cref="PromotionTask"/> is internal.
    /// </remarks>
    internal Task PromoteNowAsync(CachedFile cachedFile, string collectionName) =>
        ProcessBatchAsync([
            new PromotionTask
            {
                CachedFile = cachedFile,
                CollectionName = collectionName,
                QueuedAt = DateTime.UtcNow
            }
        ]);

    /// <summary>
    /// Promote several files in one batch, bypassing the background queue. Test seam.
    /// </summary>
    internal Task PromoteNowAsync(IReadOnlyList<CachedFile> cachedFiles, string collectionName) =>
        ProcessBatchAsync(cachedFiles
            .Select(f => new PromotionTask
            {
                CachedFile = f,
                CollectionName = collectionName,
                QueuedAt = DateTime.UtcNow
            })
            .ToList());

    /// <summary>
    /// Remove a file's chunks from L2 after it is deleted or renamed on disk.
    /// </summary>
    /// <remarks>
    /// The watcher previously only evicted from L1 on delete, so a removed file's chunks stayed in Qdrant
    /// and kept being returned by searches until someone ran a full refresh. A stale hit with no newer
    /// copy to outrank it is worse than a duplicate.
    ///
    /// Unlike promotion, this does not fall through to <see cref="GetCollectionForFile"/>'s derived-name
    /// safety net: promotion only ever adds points, so a wrong guess just lands in the wrong collection
    /// harmlessly; a delete issued against a guessed collection could silently remove someone else's
    /// data. Removal only proceeds when a collection was actually registered for this file or repository.
    /// </remarks>
    public async Task RemoveFileAsync(
        string filePath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        // Path.GetFullPath (and the resolution below) run inside the try, not before it: the caller is
        // fire-and-forget (_ = Task.Run(...)), so a throw outside this try becomes an unobserved task
        // exception that is silently dropped instead of logged.
        string? collectionName = null;
        string? relativePath = null;

        try
        {
            string normalizedPath = Path.GetFullPath(filePath);
            string normalizedRoot = Path.GetFullPath(repositoryRoot);

            if (!_fileToCollection.ContainsKey(normalizedPath) && !_fileToCollection.ContainsKey(normalizedRoot))
            {
                _logger.LogDebug("No collection registered for {File}; nothing to remove from L2", filePath);
                return;
            }

            collectionName = GetCollectionForFile(filePath, repositoryRoot);
            if (string.IsNullOrEmpty(collectionName))
            {
                _logger.LogDebug("No collection registered for {File}; nothing to remove from L2", filePath);
                return;
            }

            relativePath = IndexPath.Normalize(Path.GetRelativePath(repositoryRoot, filePath));

            if (!await _qdrantService.CollectionExistsAsync(collectionName, cancellationToken)) return;

            await _qdrantService.DeleteByFilePathAsync(collectionName, relativePath, cancellationToken);
            _logger.LogInformation("Removed {File} from collection {Collection}", relativePath, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove {File} from collection {Collection}", relativePath ?? filePath, collectionName);
        }
    }

    private void OnFileReadyForPromotion(object? sender, CachePromotionEventArgs e)
    {
        if (!_options.EnableL2Promotion) return;

        string? collectionName = GetCollectionForFile(e.CachedFile.FilePath, e.CachedFile.RepositoryRoot);
        if (collectionName == null)
        {
            Interlocked.Increment(ref _droppedPromotions);
            _logger.LogDebug("No collection registered for {File}, skipping L2 promotion",
                e.CachedFile.RelativePath);
            return;
        }

        var task = new PromotionTask
        {
            CachedFile = e.CachedFile,
            CollectionName = collectionName,
            QueuedAt = DateTime.UtcNow
        };

        // Non-blocking write - drops if queue is full
        _promotionQueue.Writer.TryWrite(task);
    }

    private async Task ProcessPromotionQueueAsync()
    {
        var batch = new List<PromotionTask>();

        try
        {
            while (!_shutdownCts.Token.IsCancellationRequested)
            {
                // Wait for first item
                PromotionTask firstTask = await _promotionQueue.Reader.ReadAsync(_shutdownCts.Token);
                batch.Add(firstTask);

                // Collect more items up to batch size (non-blocking)
                while (batch.Count < _options.L2PromotionBatchSize &&
                       _promotionQueue.Reader.TryRead(out PromotionTask? task))
                {
                    batch.Add(task);
                }

                // Process batch
                await ProcessBatchAsync(batch);
                batch.Clear();

                // Delay between batches
                await Task.Delay(_options.L2PromotionDelay, _shutdownCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("L2 promotion processing stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in L2 promotion processing");
        }
    }

    private async Task ProcessBatchAsync(List<PromotionTask> batch)
    {
        if (batch.Count == 0) return;

        _logger.LogDebug("Processing L2 promotion batch of {Count} files", batch.Count);

        // Group by collection
        IEnumerable<IGrouping<string, PromotionTask>> byCollection = batch.GroupBy(t => t.CollectionName);

        foreach (IGrouping<string, PromotionTask> group in byCollection)
        {
            string collectionName = group.Key;
            List<PromotionTask>? latestPerFile = null;

            try
            {
                // Ensure collection exists
                bool exists = await _qdrantService.CollectionExistsAsync(collectionName);
                if (!exists)
                {
                    // Every task in this group loses its write, not just one.
                    Interlocked.Add(ref _droppedPromotions, group.Count());
                    _logger.LogWarning(
                        "Collection {Collection} does not exist, dropping {Count} promotion(s). The "
                        + "affected edits are in the L1 cache only and will be lost on shutdown.",
                        collectionName, group.Count());
                    continue;
                }

                // One task per file. A batch can legitimately carry two saves of the same file, and a
                // single delete cannot stop the second task's chunks from landing beside the first's —
                // each chunking run mints fresh GUIDs, so nothing overwrites anything. Last task wins:
                // it is the most recent read of the file.
                latestPerFile = group
                    .GroupBy(t => t.CachedFile.RelativePath, StringComparer.Ordinal)
                    .Select(g => g.Last())
                    .ToList();

                // Remove each file's previous chunks before writing its new ones. Chunk ids are freshly
                // generated on every chunking run, so an upsert cannot overwrite the prior version by id;
                // without this delete each save appended another complete copy of the file, which is how
                // one method came to be returned five times at five different line ranges.
                foreach (PromotionTask task in latestPerFile)
                {
                    await _qdrantService.DeleteByFilePathAsync(collectionName, task.CachedFile.RelativePath);
                }

                // Build points for upsert
                var points = new List<(Guid id, float[] vector, Dictionary<string, object> payload)>();

                foreach (PromotionTask task in latestPerFile)
                {
                    for (var i = 0; i < task.CachedFile.Chunks.Count; i++)
                    {
                        CodeChunk chunk = task.CachedFile.Chunks[i];
                        float[] embedding = task.CachedFile.Embeddings[i];

                        var payload = new Dictionary<string, object>
                        {
                            ["file_path"] = chunk.FilePath,
                            ["relative_path"] = chunk.RelativePath,
                            ["content"] = chunk.Content,
                            ["start_line"] = chunk.StartLine,
                            ["end_line"] = chunk.EndLine,
                            ["chunk_type"] = chunk.ChunkType ?? "unknown",
                            ["symbol_name"] = chunk.SymbolName ?? "",
                            ["parent_symbol"] = chunk.ParentSymbol ?? "",
                            ["language"] = chunk.Language,
                            ["content_hash"] = chunk.ContentHash,
                            ["calls_out"] = chunk.CallsOut is { Count: > 0 }
                                ? new Value { ListValue = QdrantService.BuildCallReferenceList(chunk.CallsOut) }
                                : new Value { ListValue = new ListValue() },
                            ["calls_out_names"] = chunk.CallsOut is { Count: > 0 }
                                ? new Value { ListValue = QdrantService.BuildStringList(chunk.CallsOut.Select(c => c.MethodName).ToList()) }
                                : new Value { ListValue = new ListValue() },
                            ["return_type"] = chunk.ReturnType ?? "",
                            ["base_type"] = chunk.BaseType ?? "",
                            ["implemented_interfaces"] = chunk.ImplementedInterfaces is { Count: > 0 }
                                ? new Value { ListValue = QdrantService.BuildStringList(chunk.ImplementedInterfaces.ToList()) }
                                : new Value { ListValue = new ListValue() },
                            ["access_modifier"] = chunk.AccessModifier ?? "",
                            ["modifiers"] = chunk.Modifiers is { Count: > 0 }
                                ? new Value { ListValue = QdrantService.BuildStringList(chunk.Modifiers.ToList()) }
                                : new Value { ListValue = new ListValue() },
                            ["attributes"] = chunk.Attributes is { Count: > 0 }
                                ? new Value { ListValue = QdrantService.BuildStringList(chunk.Attributes.ToList()) }
                                : new Value { ListValue = new ListValue() },
                            ["namespace"] = chunk.Namespace ?? "",
                            ["qualified_name"] = chunk.QualifiedName ?? "",
                            ["parameters"] = chunk.Parameters is { Count: > 0 }
                                ? new Value { ListValue = QdrantService.BuildParameterList(chunk.Parameters) }
                                : new Value { ListValue = new ListValue() },
                            ["field_accesses"] = chunk.FieldAccesses is { Count: > 0 }
                                ? new Value { ListValue = QdrantService.BuildFieldAccessList(chunk.FieldAccesses) }
                                : new Value { ListValue = new ListValue() },
                            ["promoted_at"] = DateTime.UtcNow.ToString("O")
                        };

                        points.Add((chunk.Id, embedding, payload));
                    }
                }

                // Upsert to Qdrant (no re-embedding needed!)
                await _qdrantService.UpsertPointsAsync(collectionName, points);

                _logger.LogInformation("Promoted {ChunkCount} chunks from {FileCount} files to {Collection}",
                    points.Count, latestPerFile.Count, collectionName);

                // A promotion is an update. Without this, lastUpdated meant "last manual refresh" and
                // anything reading it as a freshness signal was wrong.
                await _indexStateStore.TouchAsync(collectionName, commitSha: null);
            }
            catch (Exception ex)
            {
                // A delete can succeed and be followed by a failing upsert (or a failing delete partway
                // through the loop above). Either way, files already deleted in this group now have no
                // chunks in the index at all — worse than the stale-but-present state before this catch
                // ever ran. Count them as dropped so the gap is visible instead of silent.
                Interlocked.Add(ref _droppedPromotions, latestPerFile?.Count ?? group.Count());
                _logger.LogError(ex,
                    "Error promoting batch to collection {Collection}. Up to {Count} file(s) may now be "
                    + "absent from the index rather than merely stale — their old chunks can have been "
                    + "deleted before the write failed. They recover on the next successful save or refresh.",
                    collectionName, latestPerFile?.Count ?? group.Count());
            }
        }
    }

    private string? GetCollectionForFile(string filePath, string repositoryRoot)
    {
        string normalizedPath = Path.GetFullPath(filePath);
        string normalizedRoot = Path.GetFullPath(repositoryRoot);

        // Check file-specific mapping
        if (_fileToCollection.TryGetValue(normalizedPath, out string? collection))
        {
            return collection;
        }

        // Check repository-level mapping
        if (_fileToCollection.TryGetValue(normalizedRoot, out collection))
        {
            return collection;
        }

        // Nothing registered — derive it the SAME way the indexer named the collection. This used to
        // guess "codeassist_{folder}", a shape the indexer never produces, so the lookup below could
        // only ever miss and silently drop the write. Registration now happens on every path that
        // starts watching, so this is a safety net rather than the normal route.
        string repoName = Path.GetFileName(normalizedRoot);
        return !string.IsNullOrEmpty(repoName) ? CollectionNaming.ForRepository(repoName) : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Expected on cancellation
        }

        _promotionQueue.Writer.Complete();
        _shutdownCts.Dispose();

        _logger.LogInformation("L2PromotionService disposed");
    }
}

/// <summary>
/// Represents a file queued for L2 promotion.
/// </summary>
internal class PromotionTask
{
    public required CachedFile CachedFile { get; init; }
    public required string CollectionName { get; init; }
    public required DateTime QueuedAt { get; init; }
}
