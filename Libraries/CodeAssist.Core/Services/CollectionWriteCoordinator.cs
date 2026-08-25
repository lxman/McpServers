using System.Collections.Concurrent;

namespace CodeAssist.Core.Services;

/// <summary>
/// Serializes multi-step mutations of one Qdrant collection.
/// </summary>
/// <remarks>
/// Both the repository indexer and watcher promotion path replace a file by reading its current
/// point ids, writing a new generation, and retiring the snapshot. If those sequences overlap they
/// can both snapshot the same old generation and leave two new generations behind. A collection
/// lease makes each replacement sequence atomic relative to every other writer in this process.
/// </remarks>
public sealed class CollectionWriteCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        SemaphoreSlim gate = _locks.GetOrAdd(collectionName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
