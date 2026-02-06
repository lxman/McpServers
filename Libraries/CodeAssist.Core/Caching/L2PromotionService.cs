using System.Collections.Concurrent;
using System.Threading.Channels;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Caching;

/// <summary>
/// Background service that promotes L1 cache entries to L2 (Qdrant).
/// Pre-computed embeddings from L1 are transferred without re-embedding.
/// </summary>
public sealed class L2PromotionService : IDisposable
{
    private readonly Channel<PromotionTask> _promotionQueue;
    private readonly QdrantService _qdrantService;
    private readonly CodeAssistOptions _options;
    private readonly ILogger<L2PromotionService> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _processingTask;
    private readonly ConcurrentDictionary<string, string> _fileToCollection = new(); // filePath -> collectionName
    private bool _disposed;

    public L2PromotionService(
        HotCache hotCache,
        QdrantService qdrantService,
        IOptions<CodeAssistOptions> options,
        ILogger<L2PromotionService> logger)
    {
        _qdrantService = qdrantService;
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

    private void OnFileReadyForPromotion(object? sender, CachePromotionEventArgs e)
    {
        if (!_options.EnableL2Promotion) return;

        var collectionName = GetCollectionForFile(e.CachedFile.FilePath, e.CachedFile.RepositoryRoot);
        if (collectionName == null)
        {
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
                var firstTask = await _promotionQueue.Reader.ReadAsync(_shutdownCts.Token);
                batch.Add(firstTask);

                // Collect more items up to batch size (non-blocking)
                while (batch.Count < _options.L2PromotionBatchSize &&
                       _promotionQueue.Reader.TryRead(out var task))
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
        var byCollection = batch.GroupBy(t => t.CollectionName);

        foreach (var group in byCollection)
        {
            var collectionName = group.Key;

            try
            {
                // Ensure collection exists
                var exists = await _qdrantService.CollectionExistsAsync(collectionName);
                if (!exists)
                {
                    _logger.LogWarning("Collection {Collection} does not exist, skipping promotion",
                        collectionName);
                    continue;
                }

                // Build points for upsert
                var points = new List<(Guid id, float[] vector, Dictionary<string, object> payload)>();

                foreach (var task in group)
                {
                    for (var i = 0; i < task.CachedFile.Chunks.Count; i++)
                    {
                        var chunk = task.CachedFile.Chunks[i];
                        var embedding = task.CachedFile.Embeddings[i];

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
                            ["promoted_at"] = DateTime.UtcNow.ToString("O")
                        };

                        points.Add((chunk.Id, embedding, payload));
                    }
                }

                // Upsert to Qdrant (no re-embedding needed!)
                await _qdrantService.UpsertPointsAsync(collectionName, points);

                _logger.LogInformation("Promoted {ChunkCount} chunks from {FileCount} files to {Collection}",
                    points.Count, group.Count(), collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error promoting batch to collection {Collection}", collectionName);
            }
        }
    }

    private string? GetCollectionForFile(string filePath, string repositoryRoot)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var normalizedRoot = Path.GetFullPath(repositoryRoot);

        // Check file-specific mapping
        if (_fileToCollection.TryGetValue(normalizedPath, out var collection))
        {
            return collection;
        }

        // Check repository-level mapping
        if (_fileToCollection.TryGetValue(normalizedRoot, out collection))
        {
            return collection;
        }

        // Try to derive from repository name
        var repoName = Path.GetFileName(normalizedRoot).ToLowerInvariant();
        return !string.IsNullOrEmpty(repoName) ? $"codeassist_{repoName}" : null;
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
