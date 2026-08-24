using CodeAssist.Core.Services;

namespace CodeAssist.Core.Tests.Caching;

/// <summary>
/// Records the order of calls so a test can assert that a delete precedes the upsert for a file.
/// </summary>
internal sealed class FakeQdrantWriter : IQdrantWriter
{
    public List<string> Calls { get; } = [];
    public List<string> DeletedPaths { get; } = [];
    public int UpsertedPointCount { get; private set; }
    public bool CollectionExists { get; set; } = true;

    /// <summary>What GetPointIdsByFilePathAsync reports for a path. Empty (or absent) means no points.</summary>
    public Dictionary<string, List<Guid>> ExistingPointIds { get; } = [];

    /// <summary>Ids accumulated across all DeleteByIdsAsync calls.</summary>
    public List<Guid> DeletedIds { get; } = [];

    public bool ThrowOnUpsert { get; set; }
    public bool ThrowOnDeleteIds { get; set; }
    public bool ThrowOnGetIds { get; set; }

    public Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"exists:{collectionName}");
        return Task.FromResult(CollectionExists);
    }

    public Task DeleteByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"delete:{relativePath}");
        DeletedPaths.Add(relativePath);
        return Task.CompletedTask;
    }

    public Task<List<Guid>> GetPointIdsByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"ids:{relativePath}");

        if (ThrowOnGetIds)
        {
            throw new InvalidOperationException($"Simulated failure getting point ids for {relativePath}");
        }

        List<Guid> ids = ExistingPointIds.TryGetValue(relativePath, out List<Guid>? existing)
            ? existing
            : [];
        return Task.FromResult(ids);
    }

    public Task DeleteByIdsAsync(string collectionName, IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        // Mirrors QdrantService.DeleteByIdsAsync's own short-circuit: an empty id list issues no
        // round trip in production, so the fake must not record a call for one either — otherwise
        // the call log would mean something different in tests than it does for real.
        if (ids.Count == 0) return Task.CompletedTask;

        Calls.Add($"deleteIds:{ids.Count}");

        if (ThrowOnDeleteIds)
        {
            throw new InvalidOperationException("Simulated failure deleting point ids");
        }

        DeletedIds.AddRange(ids);
        return Task.CompletedTask;
    }

    public Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"upsert:{points.Count}");

        if (ThrowOnUpsert)
        {
            throw new InvalidOperationException("Simulated failure upserting points");
        }

        UpsertedPointCount += points.Count;
        return Task.CompletedTask;
    }
}
