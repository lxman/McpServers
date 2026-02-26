namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// High-level overview of the system's data flow graph.
/// </summary>
public sealed class SystemOverview
{
    public required string CollectionName { get; init; }
    public required int TotalNodes { get; init; }
    public required int TotalEdges { get; init; }
    public required List<ComponentSummary> Components { get; init; }
    public required List<GraphEdge> CrossComponentEdges { get; init; }
    public required List<GraphNode> EntryPoints { get; init; }
}

/// <summary>
/// Summary of a logical component (typically a namespace).
/// </summary>
public sealed class ComponentSummary
{
    public required string Namespace { get; init; }
    public required int ClassCount { get; init; }
    public required int MethodCount { get; init; }
    public required int PropertyCount { get; init; }
    public required List<string> PublicSymbols { get; init; }
    public required int IncomingEdgeCount { get; init; }
    public required int OutgoingEdgeCount { get; init; }

    /// <summary>
    /// Other namespaces this component depends on.
    /// </summary>
    public required List<string> DependsOn { get; init; }

    /// <summary>
    /// Other namespaces that depend on this component.
    /// </summary>
    public required List<string> DependedOnBy { get; init; }
}
