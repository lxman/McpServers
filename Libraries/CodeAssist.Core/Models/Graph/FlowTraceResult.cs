namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// Result of tracing data flow from a symbol.
/// </summary>
public sealed class FlowTraceResult
{
    public required string StartSymbol { get; init; }
    public required string Direction { get; init; }
    public required int MaxDepth { get; init; }
    public required Dictionary<int, List<FlowStep>> StepsByDepth { get; init; }
    public required List<GraphNode> AllNodes { get; init; }
    public required List<GraphEdge> AllEdges { get; init; }
}

/// <summary>
/// A single step in a flow trace, representing a node reached at a given depth.
/// </summary>
public sealed class FlowStep
{
    public required GraphNode Node { get; init; }

    /// <summary>
    /// The edge that led us to this node (null for the root).
    /// </summary>
    public GraphEdge? IncomingEdge { get; init; }

    public required int Depth { get; init; }
}
