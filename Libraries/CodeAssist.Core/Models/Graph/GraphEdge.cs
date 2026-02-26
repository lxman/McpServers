namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// A directed edge in the data flow graph.
/// </summary>
public sealed class GraphEdge
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required GraphEdgeKind Kind { get; init; }

    /// <summary>
    /// Source line where this relationship originates (e.g., the call site).
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Human-readable label (e.g., method name, field name, HTTP route).
    /// </summary>
    public string? Label { get; init; }
}
