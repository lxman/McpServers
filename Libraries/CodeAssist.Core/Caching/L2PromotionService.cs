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
    private readonly CancellationTokenSource _delayCts = new();
    private readonly Task _processingTask;
    private readonly HotCache _hotCache;
    private readonly ConcurrentDictionary<string, string> _fileToCollection = new(); // repositoryRoot -> collectionName
    private bool _disposed;
    private int _stopping;
    private int _pendingEventEnqueues;

    public L2PromotionService(
        HotCache hotCache,
        IQdrantWriter qdrantService,
        IndexStateStore indexStateStore,
        IOptions<CodeAssistOptions> options,
        ILogger<L2PromotionService> logger)
    {
        _hotCache = hotCache;
        _qdrantService = qdrantService;
        _indexStateStore = indexStateStore;
        _options = options.Value;
        _logger = logger;

        // Backpressure preserves every accepted edit while still bounding memory.
        _promotionQueue = Channel.CreateBounded<PromotionTask>(
            new BoundedChannelOptions(Math.Max(1, _options.L2PromotionQueueCapacity))
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
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

        await _promotionQueue.Writer.WriteAsync(task, _shutdownCts.Token);
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

    private async void OnFileReadyForPromotion(object? sender, CachePromotionEventArgs e)
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

        if (Volatile.Read(ref _stopping) != 0)
        {
            Interlocked.Increment(ref _droppedPromotions);
            return;
        }

        Interlocked.Increment(ref _pendingEventEnqueues);
        try
        {
            await _promotionQueue.Writer.WriteAsync(task, _shutdownCts.Token);
        }
        catch (Exception ex) when (ex is ChannelClosedException or OperationCanceledException)
        {
            Interlocked.Increment(ref _droppedPromotions);
            _logger.LogWarning("Promotion for {File} was rejected while the service was stopping",
                e.CachedFile.RelativePath);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingEventEnqueues);
        }
    }

    private async Task ProcessPromotionQueueAsync()
    {
        var batch = new List<PromotionTask>();

        try
        {
            while (await _promotionQueue.Reader.WaitToReadAsync(_shutdownCts.Token))
            {
                // Wait for first item
                if (!_promotionQueue.Reader.TryRead(out PromotionTask? firstTask)) continue;
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

                if (Volatile.Read(ref _stopping) == 0)
                {
                    try
                    {
                        await Task.Delay(_options.L2PromotionDelay, _delayCts.Token);
                    }
                    catch (OperationCanceledException) when (Volatile.Read(ref _stopping) != 0)
                    {
                        // Shutdown skips throttling so accepted work can drain promptly.
                    }
                }
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

                // A promotion queued before a delete can land after it and resurrect the file. Chunking
                // and embedding take seconds, so this window is wide, and the result is worse than a
                // duplicate: a stale hit with no newer copy to outrank it, surviving until a full
                // reindex.
                List<PromotionTask> stillOnDisk = latestPerFile
                    .Where(t => File.Exists(t.CachedFile.FilePath))
                    .ToList();

                if (stillOnDisk.Count < latestPerFile.Count)
                {
                    // Deliberately not LogDebug, and deliberately not counted as a dropped promotion.
                    // File.Exists returns false for any failure, not only for deletion — a network
                    // share that hiccups for a moment looks identical to a file the user removed. In
                    // that case this is a real edit discarded, living in L1 only until the process
                    // exits, so it has to be visible at default log levels. It is not a dropped
                    // promotion because the ordinary case really is a deleted file, and inflating that
                    // counter would make DroppedPromotionCount mean something other than what
                    // get_watched_repositories reports it to mean.
                    _logger.LogInformation(
                        "Skipping {Count} promotion(s) for file(s) no longer on disk in {Collection}. "
                        + "Normally these were deleted after being queued; if the repository is on a "
                        + "network path, a transient unavailability looks the same and the edit is lost.",
                        latestPerFile.Count - stillOnDisk.Count, collectionName);
                }

                latestPerFile = stillOnDisk;
                if (latestPerFile.Count == 0) continue;

                // Learn each file's prior point ids before writing anything. The write must come first
                // and the delete must come after: chunk ids are freshly generated on every chunking run,
                // so a file's new points can never collide with its old ones, and the two generations can
                // safely coexist for the moment between the upsert and the delete. Reading the ids now,
                // before the upsert, is required rather than incidental — once the new chunks have been
                // written, the old and new generations are both filed under the same relative_path and
                // are no longer distinguishable by path, so this is the last point at which "old" can be
                // known at all.
                //
                // A file whose ids cannot be read is dropped from this batch entirely rather than upserted
                // anyway: writing it without knowing what to delete would leave its old chunks behind
                // forever, indistinguishable from the new ones. Its old chunks simply stay in place —
                // stale, not absent.
                var oldPointIds = new List<Guid>();
                var toPromote = new List<PromotionTask>();

                foreach (PromotionTask task in latestPerFile)
                {
                    try
                    {
                        List<Guid> ids = await _qdrantService.GetPointIdsByFilePathAsync(
                            collectionName, task.CachedFile.RelativePath);
                        oldPointIds.AddRange(ids);
                        toPromote.Add(task);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to read existing point ids for {File} in {Collection}; skipping this "
                            + "file's promotion. Its previously indexed chunks remain in place.",
                            task.CachedFile.RelativePath, collectionName);
                    }
                }

                latestPerFile = toPromote;
                if (latestPerFile.Count == 0) continue;

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
                            ["canonical_symbol_name"] = chunk.SymbolName is { Length: > 0 } symbolName
                                ? SearchResultDiversifier.RemovePartSuffix(symbolName)
                                : "",
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
                            ["canonical_qualified_name"] = chunk.QualifiedName is { Length: > 0 } qualifiedName
                                ? SearchResultDiversifier.RemovePartSuffix(qualifiedName)
                                : "",
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

                // Only now, after the write has landed, remove the superseded generation. Failure here
                // is deliberately swallowed rather than rethrown or counted as a dropped promotion: the
                // promotion itself succeeded, and leaving both generations present is self-healing — the
                // next promotion of any of these files reads ids that include this leftover batch and
                // deletes it too. Rethrowing would misreport a successful write as a lost promotion.
                try
                {
                    await _qdrantService.DeleteByIdsAsync(collectionName, oldPointIds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to remove {Count} superseded point(s) after promoting to {Collection}. "
                        + "Both the old and new generations of the affected file(s) are now present; the "
                        + "next promotion of any of them will clear the leftovers.",
                        oldPointIds.Count, collectionName);
                }
            }
            catch (Exception ex)
            {
                // Reaching here means the write itself never landed — the id-collection pass only reads,
                // and the old generation is not removed until after a successful upsert (in its own
                // try/catch below, which never rethrows). So a failure caught here leaves every file in
                // this group exactly as it was: stale, not absent. Still counted as dropped, because the
                // L1 edit did not reach the index and is lost if the process exits before a retry.
                Interlocked.Add(ref _droppedPromotions, latestPerFile?.Count ?? group.Count());
                _logger.LogError(ex,
                    "Error promoting batch to collection {Collection}. Up to {Count} file(s) were not "
                    + "written; their previously indexed chunks remain in place. They recover on the next "
                    + "successful save or refresh.",
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

        Interlocked.Exchange(ref _stopping, 1);
        _hotCache.FileReadyForPromotion -= OnFileReadyForPromotion;
        _delayCts.Cancel();

        // Event handlers may already be waiting for channel capacity. Keep the reader alive until
        // they have either enqueued or observed shutdown, then close the writer and drain the queue.
        var spinner = new SpinWait();
        while (Volatile.Read(ref _pendingEventEnqueues) != 0)
        {
            spinner.SpinOnce();
        }

        _promotionQueue.Writer.TryComplete();

        try
        {
            _processingTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }

        _shutdownCts.Cancel();
        _delayCts.Dispose();
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
