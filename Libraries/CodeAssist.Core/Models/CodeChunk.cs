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
    /// Methods/functions called from this chunk, with receiver and location info.
    /// </summary>
    public IReadOnlyList<CallReference>? CallsOut { get; init; }

    /// <summary>
    /// Programming language of this chunk.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// SHA256 hash of the content for change detection.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Parameters of the method/function, or null for non-callable chunks.
    /// </summary>
    public IReadOnlyList<ParameterInfo>? Parameters { get; init; }

    /// <summary>
    /// Return type of the method/function, or null if not applicable.
    /// </summary>
    public string? ReturnType { get; init; }

    /// <summary>
    /// Base type this type extends, or null.
    /// </summary>
    public string? BaseType { get; init; }

    /// <summary>
    /// Interfaces this type implements, or null.
    /// </summary>
    public IReadOnlyList<string>? ImplementedInterfaces { get; init; }

    /// <summary>
    /// Access modifier (public, private, protected, internal), or null if not extracted.
    /// </summary>
    public string? AccessModifier { get; init; }

    /// <summary>
    /// Additional modifiers (static, abstract, virtual, async, sealed, etc.), or null.
    /// </summary>
    public IReadOnlyList<string>? Modifiers { get; init; }

    /// <summary>
    /// Attributes or decorators applied to this declaration, or null.
    /// </summary>
    public IReadOnlyList<string>? Attributes { get; init; }

    /// <summary>
    /// Fields and properties accessed within this chunk, or null.
    /// </summary>
    public IReadOnlyList<FieldAccess>? FieldAccesses { get; init; }

    /// <summary>
    /// Namespace or module this chunk belongs to, or null.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Fully qualified name (e.g., "Namespace.Class.Method"), or null.
    /// </summary>
    public string? QualifiedName { get; init; }
}
