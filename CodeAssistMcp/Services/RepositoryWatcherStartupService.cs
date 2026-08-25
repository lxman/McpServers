using System.Collections.Concurrent;
using System.Threading.Channels;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeAssistMcp.Services;

/// <summary>
/// Restores the active repository's watcher after a process restart.
/// </summary>
public sealed class RepositoryWatcherStartupService(
    ActiveRepositoryStore activeRepositoryStore,
    RepositoryIndexer indexer,
    FileWatcherService fileWatcher,
    L2PromotionService l2Promotion,
    ILogger<RepositoryWatcherStartupService> logger) : IHostedService
{
    private readonly Channel<string> _reconciliationQueue = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<string, string> _queuedTriggers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IndexState> _knownRepositories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RepositoryReconciliationStatus> _statuses =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdownCts = new();
    private Task? _workerTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        fileWatcher.ReconciliationRequested += OnReconciliationRequested;
        _workerTask = Task.Run(() => ProcessReconciliationQueueAsync(_shutdownCts.Token));

        string? repositoryName = activeRepositoryStore.TryLoad();
        if (repositoryName is null) return;

        try
        {
            IndexState? state = await indexer.GetIndexStateAsync(repositoryName, cancellationToken);
            if (state is null)
            {
                logger.LogWarning(
                    "Cannot restore watcher for {Repository}: its index state no longer exists",
                    repositoryName);
                activeRepositoryStore.TryClear();
                return;
            }

            if (!Directory.Exists(state.RootPath))
            {
                logger.LogWarning(
                    "Cannot restore watcher for {Repository}: repository path {Path} does not exist",
                    repositoryName, state.RootPath);
                return;
            }

            fileWatcher.WatchRepository(state.RootPath, state.IncludePatterns, state.ExcludePatterns);
            l2Promotion.RegisterRepositoryCollection(state.RootPath, state.CollectionName);
            RequestReconciliation(state, "startup");
            logger.LogInformation("Restored watcher for active repository {Repository} at {Path}",
                repositoryName, state.RootPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A bad persisted watcher must remain diagnosable without preventing the MCP server from
            // starting and serving explicit repair/status tools.
            logger.LogError(ex, "Failed to restore watcher for active repository {Repository}", repositoryName);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        fileWatcher.ReconciliationRequested -= OnReconciliationRequested;
        _reconciliationQueue.Writer.TryComplete();
        _shutdownCts.Cancel();

        if (_workerTask is null) return;
        try
        {
            await _workerTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when either host shutdown token wins.
        }
    }

    public RepositoryReconciliationStatus? GetStatus(string repositoryRoot) =>
        _statuses.GetValueOrDefault(NormalizeRoot(repositoryRoot));

    public void RequestReconciliation(IndexState state, string trigger)
    {
        _knownRepositories[NormalizeRoot(state.RootPath)] = state;
        QueueReconciliation(state.RootPath, trigger);
    }

    private void OnReconciliationRequested(
        object? sender,
        RepositoryReconciliationRequestedEventArgs e) =>
        QueueReconciliation(e.RepositoryRoot, "watcher-error");

    private void QueueReconciliation(string repositoryRoot, string trigger)
    {
        string normalizedRoot = NormalizeRoot(repositoryRoot);
        if (!_queuedTriggers.TryAdd(normalizedRoot, trigger)) return;

        _statuses[normalizedRoot] = new RepositoryReconciliationStatus
        {
            State = "queued",
            Trigger = trigger,
            RequestedAt = DateTimeOffset.UtcNow
        };
        if (!_reconciliationQueue.Writer.TryWrite(normalizedRoot))
        {
            _queuedTriggers.TryRemove(normalizedRoot, out _);
            _statuses[normalizedRoot] = _statuses[normalizedRoot] with
            {
                State = "failed",
                CompletedAt = DateTimeOffset.UtcNow,
                Error = "The reconciliation worker is no longer accepting work."
            };
        }
    }

    private async Task ProcessReconciliationQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (string root in _reconciliationQueue.Reader.ReadAllAsync(cancellationToken))
            {
                if (!_queuedTriggers.TryRemove(root, out string? trigger)) continue;
                await ReconcileAsync(root, trigger, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }

    private async Task ReconcileAsync(
        string repositoryRoot,
        string trigger,
        CancellationToken cancellationToken)
    {
        RepositoryReconciliationStatus queued = _statuses[repositoryRoot];
        _statuses[repositoryRoot] = queued with
        {
            State = "running",
            StartedAt = DateTimeOffset.UtcNow
        };

        try
        {
            IndexState? state = await ResolveStateAsync(repositoryRoot, cancellationToken);
            if (state is null)
                throw new InvalidOperationException($"No index state maps to repository root '{repositoryRoot}'.");

            logger.LogInformation("Reconciling {Repository} after {Trigger}", state.RepositoryName, trigger);
            IndexingResult result = await indexer.IndexRepositoryAsync(
                state.RootPath,
                state.RepositoryName,
                state.IncludePatterns,
                state.ExcludePatterns,
                cancellationToken);

            string finalState = !result.Success
                ? "failed"
                : result.FailedFiles.Count > 0 ? "partial" : "succeeded";
            _statuses[repositoryRoot] = _statuses[repositoryRoot] with
            {
                State = finalState,
                CompletedAt = DateTimeOffset.UtcNow,
                FilesProcessed = result.FilesProcessed,
                FailedFiles = result.FailedFiles,
                Error = result.ErrorMessage
            };

            if (finalState == "succeeded")
            {
                logger.LogInformation(
                    "Reconciled {Repository}: {Processed} changed file(s), {Skipped} unchanged",
                    state.RepositoryName, result.FilesProcessed, result.FilesSkipped);
            }
            else
            {
                logger.LogWarning(
                    "Reconciliation for {Repository} finished as {State}: {Error}; failed files: {FailedFiles}",
                    state.RepositoryName, finalState, result.ErrorMessage,
                    string.Join(", ", result.FailedFiles));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _statuses[repositoryRoot] = _statuses[repositoryRoot] with
            {
                State = "cancelled",
                CompletedAt = DateTimeOffset.UtcNow
            };
            throw;
        }
        catch (Exception ex)
        {
            _statuses[repositoryRoot] = _statuses[repositoryRoot] with
            {
                State = "failed",
                CompletedAt = DateTimeOffset.UtcNow,
                Error = ex.Message
            };
            logger.LogError(ex, "Failed to reconcile repository at {RepositoryRoot}", repositoryRoot);
        }
    }

    private async Task<IndexState?> ResolveStateAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        if (_knownRepositories.TryGetValue(repositoryRoot, out IndexState? known)) return known;

        List<string?> names = await indexer.ListIndexedRepositoriesAsync(cancellationToken);
        foreach (string? name in names.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            IndexState? state = await indexer.GetIndexStateAsync(name!, cancellationToken);
            if (state is null) continue;

            string normalizedStateRoot = NormalizeRoot(state.RootPath);
            _knownRepositories[normalizedStateRoot] = state;
            if (normalizedStateRoot.Equals(repositoryRoot, StringComparison.OrdinalIgnoreCase))
                return state;
        }

        return null;
    }

    private static string NormalizeRoot(string repositoryRoot) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
}

public sealed record RepositoryReconciliationStatus
{
    public required string State { get; init; }
    public required string Trigger { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int? FilesProcessed { get; init; }
    public IReadOnlyList<string>? FailedFiles { get; init; }
    public string? Error { get; init; }
}
