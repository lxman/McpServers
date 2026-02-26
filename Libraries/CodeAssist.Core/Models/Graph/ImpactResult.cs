namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// Result of an impact analysis — what is affected if a given symbol changes.
/// </summary>
public sealed class ImpactResult
{
    public required string SourceSymbol { get; init; }
    public required List<ImpactedNode> DirectlyAffected { get; init; }
    public required List<ImpactedNode> TransitivelyAffected { get; init; }
    public int TotalAffectedCount => DirectlyAffected.Count + TransitivelyAffected.Count;
}

/// <summary>
/// A node impacted by a change, with the relationship and distance.
/// </summary>
public sealed class ImpactedNode
{
    public required GraphNode Node { get; init; }
    public required GraphEdgeKind Relationship { get; init; }
    public required int Distance { get; init; }
}
