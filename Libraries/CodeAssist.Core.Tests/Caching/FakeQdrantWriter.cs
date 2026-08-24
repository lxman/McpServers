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

    public Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"upsert:{points.Count}");
        UpsertedPointCount += points.Count;
        return Task.CompletedTask;
    }
}
