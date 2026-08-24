namespace CodeAssist.Core.Services;

/// <summary>
/// The narrow slice of Qdrant that the promotion path writes through.
/// </summary>
/// <remarks>
/// Exists so the write-before-delete ordering on the promotion path can be asserted without a live
/// Qdrant. That ordering is not cosmetic: chunk ids are freshly generated on every chunking run, so a
/// file's new points can never collide with its old ones by id. Promotion reads the old generation's
/// ids, upserts the new generation, and only then deletes the old ids — a failed upsert leaves the
/// file's prior chunks in place (stale) rather than gone (absent), which is what deleting first
/// produced.
/// </remarks>
public interface IQdrantWriter
{
    Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default);

    Task DeleteByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default);

    Task<List<Guid>> GetPointIdsByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default);

    Task DeleteByIdsAsync(string collectionName, IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points,
        CancellationToken cancellationToken = default);
}
