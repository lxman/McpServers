namespace CodeAssist.Core.Models;

/// <summary>
/// Represents a chunk of code extracted from a source file.
/// </summary>
public sealed record CodeChunk
{
    /// <summary>
    /// Unique identifier for this chunk.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Absolute path to the source file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Path relative to the repository root.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The actual code content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Starting line number (1-indexed).
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Ending line number (1-indexed).
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// Type of chunk (e.g., "class", "method", "property", "file").
    /// </summary>
    public required string ChunkType { get; init; }

    /// <summary>
    /// Name of the symbol (class name, method name, etc.) or null for file-level chunks.
    /// </summary>
    public string? SymbolName { get; init; }

    /// <summary>
    /// Parent symbol name (e.g., class name for a method) or null.
    /// </summary>
    public string? ParentSymbol { get; init; }

    /// <summary>
    /// Names of functions/methods called from this chunk, or null if not extracted.
    /// </summary>
    public IReadOnlyList<string>? CallsOut { get; init; }

    /// <summary>
    /// Programming language of this chunk.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// SHA256 hash of the content for change detection.
    /// </summary>
    public required string ContentHash { get; init; }
}
