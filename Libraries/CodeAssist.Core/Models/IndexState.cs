namespace CodeAssist.Core.Models;

/// <summary>
/// Tracks the overall state of an indexed repository.
/// </summary>
public sealed record IndexState
{
    /// <summary>
    /// Unique name for this repository index.
    /// </summary>
    public required string RepositoryName { get; init; }

    /// <summary>
    /// Root path of the repository.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Git commit SHA at time of last index (null if not a git repo).
    /// </summary>
    public string? LastCommitSha { get; init; }

    /// <summary>
    /// When the index was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the index was last updated.
    /// </summary>
    public required DateTimeOffset LastUpdatedAt { get; init; }

    /// <summary>
    /// Total number of files indexed.
    /// </summary>
    public required int FileCount { get; init; }

    /// <summary>
    /// Total number of chunks in the index.
    /// </summary>
    public required int ChunkCount { get; init; }

    /// <summary>
    /// Embedding model used for this index.
    /// </summary>
    public required string EmbeddingModel { get; init; }

    /// <summary>
    /// Qdrant collection name for this repository.
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// File patterns that were included.
    /// </summary>
    public required IReadOnlyList<string> IncludePatterns { get; init; }

    /// <summary>
    /// File patterns that were excluded.
    /// </summary>
    public required IReadOnlyList<string> ExcludePatterns { get; init; }
}

/// <summary>
/// Result of an indexing operation.
/// </summary>
public sealed record IndexingResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Number of files processed.
    /// </summary>
    public required int FilesProcessed { get; init; }

    /// <summary>
    /// Number of files added.
    /// </summary>
    public required int FilesAdded { get; init; }

    /// <summary>
    /// Number of files updated.
    /// </summary>
    public required int FilesUpdated { get; init; }

    /// <summary>
    /// Number of files removed.
    /// </summary>
    public required int FilesRemoved { get; init; }

    /// <summary>
    /// Number of files skipped (unchanged).
    /// </summary>
    public required int FilesSkipped { get; init; }

    /// <summary>
    /// Total chunks in the index after operation.
    /// </summary>
    public required int TotalChunks { get; init; }

    /// <summary>
    /// Time taken for the operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Files that failed to process.
    /// </summary>
    public IReadOnlyList<string> FailedFiles { get; init; } = [];
}
