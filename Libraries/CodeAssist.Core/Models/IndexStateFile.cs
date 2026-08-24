namespace CodeAssist.Core.Models;

/// <summary>
/// The on-disk index state for one repository.
/// </summary>
/// <remarks>
/// Public rather than nested-private because more than one writer updates an index: the full indexer
/// rewrites it wholesale, and the promotion path advances its freshness stamps.
/// </remarks>
public sealed record IndexStateFile
{
    public required string RepositoryName { get; init; }
    public required string RootPath { get; init; }
    public string? LastCommitSha { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastUpdatedAt { get; init; }
    public required string EmbeddingModel { get; init; }
    public required string CollectionName { get; init; }
    public required List<string> IncludePatterns { get; init; }
    public required List<string> ExcludePatterns { get; init; }
    public required Dictionary<string, IndexedFile> Files { get; init; }
}
