namespace CodeAssist.Core.Services;

/// <summary>
/// The narrow slice of Qdrant that the promotion path writes through.
/// </summary>
/// <remarks>
/// Exists so the delete-then-upsert ordering on the promotion path can be asserted without a live
/// Qdrant. That ordering is not cosmetic: chunk ids are freshly generated on every chunking run, so an
/// upsert can never overwrite the previous version of a file by id, and a promotion without a preceding
/// delete appends a complete additional copy of the file every time it is saved.
/// </remarks>
public interface IQdrantWriter
{
    Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default);

    Task DeleteByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default);

    Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points,
        CancellationToken cancellationToken = default);
}
