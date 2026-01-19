namespace CodeAssist.Core.Models;

/// <summary>
/// Tracks metadata about an indexed file for change detection.
/// </summary>
public sealed record IndexedFile
{
    /// <summary>
    /// Path relative to the repository root.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// SHA256 hash of the file content.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Last modified timestamp of the file when indexed.
    /// </summary>
    public required DateTimeOffset LastModified { get; init; }

    /// <summary>
    /// When this file was indexed.
    /// </summary>
    public required DateTimeOffset IndexedAt { get; init; }

    /// <summary>
    /// Number of chunks generated from this file.
    /// </summary>
    public required int ChunkCount { get; init; }

    /// <summary>
    /// IDs of chunks belonging to this file (for deletion on update).
    /// </summary>
    public required IReadOnlyList<Guid> ChunkIds { get; init; }
}
