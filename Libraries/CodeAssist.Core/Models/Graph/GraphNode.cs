namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// A node in the data flow graph, representing a code symbol.
/// </summary>
public sealed class GraphNode
{
    /// <summary>
    /// Stable identity for this node. Typically the qualified name,
    /// falling back to a constructed key from namespace + parent + symbol.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The Qdrant chunk ID this node was derived from.
    /// </summary>
    public required Guid ChunkId { get; init; }

    public required string SymbolName { get; init; }
    public string? QualifiedName { get; init; }
    public string? Namespace { get; init; }
    public string? ChunkType { get; init; }
    public string? FilePath { get; init; }
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public string? ParentSymbol { get; init; }
    public string? ReturnType { get; init; }
    public string? BaseType { get; init; }
    public IReadOnlyList<string>? ImplementedInterfaces { get; init; }
    public string? AccessModifier { get; init; }
    public IReadOnlyList<string>? Modifiers { get; init; }

    /// <summary>
    /// True if this node was created as a placeholder for an unresolved reference
    /// (e.g., a call target in an external package).
    /// </summary>
    public bool IsPhantom { get; init; }
}
