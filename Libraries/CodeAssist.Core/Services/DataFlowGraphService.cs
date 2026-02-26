using System.Collections.Concurrent;
using CodeAssist.Core.Models;
using CodeAssist.Core.Models.Graph;
using Microsoft.Extensions.Logging;

namespace CodeAssist.Core.Services;

/// <summary>
/// Builds and traverses the complete data flow graph for indexed repositories.
/// The graph is an in-memory directed graph derived from Qdrant data.
/// </summary>
public sealed class DataFlowGraphService
{
    private readonly QdrantService _qdrant;
    private readonly ILogger<DataFlowGraphService> _logger;
    private readonly ConcurrentDictionary<string, CodeGraph> _graphs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new(StringComparer.OrdinalIgnoreCase);

    public DataFlowGraphService(
        QdrantService qdrant,
        ILogger<DataFlowGraphService> logger)
    {
        _qdrant = qdrant;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────────
    //  5a — Graph Construction
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the full data flow graph for a collection by scrolling all chunks from Qdrant.
    /// </summary>
    public async Task<CodeGraph> BuildGraphAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim buildLock = _buildLocks.GetOrAdd(collectionName, _ => new SemaphoreSlim(1, 1));
        await buildLock.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Building data flow graph for {Collection}", collectionName);

            List<CodeChunk> chunks = await _qdrant.ScrollAllChunksAsync(collectionName, cancellationToken);

            var graph = new CodeGraph();
            BuildNodesFromChunks(graph, chunks);
            BuildEdgesFromChunks(graph, chunks);

            _graphs[collectionName] = graph;

            _logger.LogInformation(
                "Built graph for {Collection}: {Nodes} nodes, {Edges} edges",
                collectionName, graph.NodeCount, graph.EdgeCount);

            return graph;
        }
        finally
        {
            buildLock.Release();
        }
    }

    /// <summary>
    /// Rebuild the graph for a single file (incremental update).
    /// Removes old nodes/edges for the file and re-adds from fresh Qdrant data.
    /// </summary>
    public async Task RebuildFileAsync(
        string collectionName,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!_graphs.TryGetValue(collectionName, out CodeGraph? graph))
        {
            _logger.LogDebug("No graph for {Collection}, skipping file rebuild", collectionName);
            return;
        }

        SemaphoreSlim buildLock = _buildLocks.GetOrAdd(collectionName, _ => new SemaphoreSlim(1, 1));
        await buildLock.WaitAsync(cancellationToken);

        try
        {
            // Remove old nodes for this file
            graph.RemoveNodesByFile(relativePath);

            // Fetch fresh chunks for this file from Qdrant
            List<SearchResult> results = await _qdrant.SearchByFilePathAsync(
                collectionName, relativePath, cancellationToken);

            List<CodeChunk> chunks = results.Select(r => r.Chunk).ToList();

            BuildNodesFromChunks(graph, chunks);
            BuildEdgesFromChunks(graph, chunks);

            _logger.LogDebug("Rebuilt graph for file {File} in {Collection}: {Nodes} nodes",
                relativePath, collectionName, chunks.Count);
        }
        finally
        {
            buildLock.Release();
        }
    }

    /// <summary>
    /// Get the cached graph for a collection, or null if not yet built.
    /// </summary>
    public CodeGraph? GetGraph(string collectionName)
    {
        return _graphs.GetValueOrDefault(collectionName);
    }

    // ────────────────────────────────────────────────────────────────
    //  5b — Graph Traversal
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trace data flow forward from a symbol: what does it call, what data does it pass?
    /// </summary>
    public FlowTraceResult TraceForward(string collectionName, string symbolId, int maxDepth = 5)
    {
        return TraceFlow(collectionName, symbolId, maxDepth, forward: true);
    }

    /// <summary>
    /// Trace data flow backward from a symbol: who calls it and with what data?
    /// </summary>
    public FlowTraceResult TraceBackward(string collectionName, string symbolId, int maxDepth = 5)
    {
        return TraceFlow(collectionName, symbolId, maxDepth, forward: false);
    }

    /// <summary>
    /// Trace data flow in both directions from a symbol.
    /// </summary>
    public FlowTraceResult TraceFullFlow(string collectionName, string symbolId, int maxDepth = 5)
    {
        FlowTraceResult forward = TraceFlow(collectionName, symbolId, maxDepth, forward: true);
        FlowTraceResult backward = TraceFlow(collectionName, symbolId, maxDepth, forward: false);

        // Merge results
        var allNodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var allEdges = new List<GraphEdge>();
        var stepsByDepth = new Dictionary<int, List<FlowStep>>();

        // Backward steps get negative depth
        foreach ((int depth, List<FlowStep> steps) in backward.StepsByDepth)
        {
            stepsByDepth[-depth] = steps;
            foreach (FlowStep step in steps)
                allNodes.TryAdd(step.Node.Id, step.Node);
        }

        // Forward steps keep positive depth
        foreach ((int depth, List<FlowStep> steps) in forward.StepsByDepth)
        {
            stepsByDepth[depth] = steps;
            foreach (FlowStep step in steps)
                allNodes.TryAdd(step.Node.Id, step.Node);
        }

        allEdges.AddRange(backward.AllEdges);
        allEdges.AddRange(forward.AllEdges);

        return new FlowTraceResult
        {
            StartSymbol = symbolId,
            Direction = "both",
            MaxDepth = maxDepth,
            StepsByDepth = stepsByDepth,
            AllNodes = allNodes.Values.ToList(),
            AllEdges = allEdges
        };
    }

    /// <summary>
    /// Analyze the impact of changing a symbol: what else is affected?
    /// Traces backward (who calls this?) and forward through inheritance/implementation.
    /// </summary>
    public ImpactResult AnalyzeImpact(string collectionName, string symbolId, int maxDepth = 5)
    {
        CodeGraph graph = GetGraphOrThrow(collectionName);
        IReadOnlyList<string> resolvedIds = graph.ResolveSymbol(symbolId);
        if (resolvedIds.Count == 0)
        {
            return new ImpactResult
            {
                SourceSymbol = symbolId,
                DirectlyAffected = [],
                TransitivelyAffected = []
            };
        }

        string startId = resolvedIds[0];
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startId };
        var directlyAffected = new List<ImpactedNode>();
        var transitivelyAffected = new List<ImpactedNode>();

        // BFS backward through callers + forward through overrides/implementations
        var queue = new Queue<(string nodeId, int distance)>();
        queue.Enqueue((startId, 0));

        while (queue.Count > 0)
        {
            (string currentId, int distance) = queue.Dequeue();

            // Callers (backward through Calls edges)
            foreach (GraphEdge inEdge in graph.GetIncomingEdges(currentId))
            {
                if (!visited.Add(inEdge.SourceId)) continue;
                GraphNode? node = graph.GetNode(inEdge.SourceId);
                if (node == null) continue;

                int newDistance = distance + 1;
                var impacted = new ImpactedNode
                {
                    Node = node,
                    Relationship = inEdge.Kind,
                    Distance = newDistance
                };

                if (newDistance == 1)
                    directlyAffected.Add(impacted);
                else
                    transitivelyAffected.Add(impacted);

                if (newDistance < maxDepth)
                    queue.Enqueue((inEdge.SourceId, newDistance));
            }

            // Subclasses / implementors (forward through Inherits/Implements edges targeting us)
            foreach (GraphEdge inEdge in graph.GetIncomingEdges(currentId))
            {
                if (inEdge.Kind is not (GraphEdgeKind.Inherits or GraphEdgeKind.Implements))
                    continue;
                if (!visited.Add(inEdge.SourceId)) continue;

                GraphNode? node = graph.GetNode(inEdge.SourceId);
                if (node == null) continue;

                int newDistance = distance + 1;
                var impacted = new ImpactedNode
                {
                    Node = node,
                    Relationship = inEdge.Kind,
                    Distance = newDistance
                };

                if (newDistance == 1)
                    directlyAffected.Add(impacted);
                else
                    transitivelyAffected.Add(impacted);

                if (newDistance < maxDepth)
                    queue.Enqueue((inEdge.SourceId, newDistance));
            }
        }

        return new ImpactResult
        {
            SourceSymbol = symbolId,
            DirectlyAffected = directlyAffected,
            TransitivelyAffected = transitivelyAffected
        };
    }

    /// <summary>
    /// Detect all cycles in the call graph.
    /// Returns a list of cycles, where each cycle is the list of node IDs forming the loop.
    /// </summary>
    public List<List<string>> DetectCycles(string collectionName)
    {
        CodeGraph graph = GetGraphOrThrow(collectionName);

        var cycles = new List<List<string>>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();

        foreach (GraphNode node in graph.GetAllNodes())
        {
            if (!visited.Contains(node.Id))
                DfsCycleDetect(graph, node.Id, visited, onStack, stack, cycles);
        }

        return cycles;
    }

    /// <summary>
    /// Check if there is any path from one symbol to another.
    /// </summary>
    public bool IsReachable(string collectionName, string fromSymbol, string toSymbol)
    {
        CodeGraph graph = GetGraphOrThrow(collectionName);

        IReadOnlyList<string> fromIds = graph.ResolveSymbol(fromSymbol);
        IReadOnlyList<string> toIds = graph.ResolveSymbol(toSymbol);
        if (fromIds.Count == 0 || toIds.Count == 0) return false;

        var targetSet = new HashSet<string>(toIds, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (string id in fromIds)
            queue.Enqueue(id);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (targetSet.Contains(current)) return true;
            if (!visited.Add(current)) continue;

            foreach (GraphEdge edge in graph.GetOutgoingEdges(current))
            {
                if (!visited.Contains(edge.TargetId))
                    queue.Enqueue(edge.TargetId);
            }
        }

        return false;
    }

    /// <summary>
    /// Find entry points: public methods/classes with no incoming Calls edges.
    /// These are likely API controllers, event handlers, Main methods, etc.
    /// </summary>
    public List<GraphNode> FindEntryPoints(string collectionName)
    {
        CodeGraph graph = GetGraphOrThrow(collectionName);

        return graph.GetAllNodes()
            .Where(n => !n.IsPhantom)
            .Where(n => n.ChunkType is "method" or "class")
            .Where(n => n.AccessModifier is "public" or null)
            .Where(n => graph.GetIncomingEdges(n.Id)
                .All(e => e.Kind is not GraphEdgeKind.Calls))
            .ToList();
    }

    // ────────────────────────────────────────────────────────────────
    //  5d — Summarization for UI
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generate a high-level overview of the system.
    /// </summary>
    public SystemOverview GetSystemOverview(string collectionName)
    {
        CodeGraph graph = GetGraphOrThrow(collectionName);

        IReadOnlyList<string> namespaces = graph.GetAllNamespaces();
        var components = new List<ComponentSummary>();
        var crossComponentEdges = new List<GraphEdge>();

        foreach (string ns in namespaces)
        {
            ComponentSummary component = BuildComponentSummary(graph, ns);
            components.Add(component);
        }

        // Find cross-component edges (edges between different namespaces)
        foreach (GraphNode node in graph.GetAllNodes())
        {
            foreach (GraphEdge edge in graph.GetOutgoingEdges(node.Id))
            {
                GraphNode? target = graph.GetNode(edge.TargetId);
                if (target == null) continue;

                if (!string.IsNullOrEmpty(node.Namespace) &&
                    !string.IsNullOrEmpty(target.Namespace) &&
                    !string.Equals(node.Namespace, target.Namespace, StringComparison.OrdinalIgnoreCase))
                {
                    crossComponentEdges.Add(edge);
                }
            }
        }

        return new SystemOverview
        {
            CollectionName = collectionName,
            TotalNodes = graph.NodeCount,
            TotalEdges = graph.EdgeCount,
            Components = components.OrderByDescending(c => c.MethodCount).ToList(),
            CrossComponentEdges = crossComponentEdges,
            EntryPoints = FindEntryPoints(collectionName)
        };
    }

    /// <summary>
    /// Get detailed information about a component (namespace).
    /// </summary>
    public ComponentSummary GetComponentDetail(string collectionName, string namespaceName)
    {
        CodeGraph graph = GetGraphOrThrow(collectionName);
        return BuildComponentSummary(graph, namespaceName);
    }

    // ────────────────────────────────────────────────────────────────
    //  Private: Graph construction helpers
    // ────────────────────────────────────────────────────────────────

    private static void BuildNodesFromChunks(CodeGraph graph, List<CodeChunk> chunks)
    {
        foreach (CodeChunk chunk in chunks)
        {
            string nodeId = BuildNodeId(chunk);
            if (string.IsNullOrEmpty(nodeId)) continue;

            var node = new GraphNode
            {
                Id = nodeId,
                ChunkId = chunk.Id,
                SymbolName = chunk.SymbolName ?? "",
                QualifiedName = chunk.QualifiedName,
                Namespace = chunk.Namespace,
                ChunkType = chunk.ChunkType,
                FilePath = chunk.RelativePath,
                StartLine = chunk.StartLine,
                EndLine = chunk.EndLine,
                ParentSymbol = chunk.ParentSymbol,
                ReturnType = chunk.ReturnType,
                BaseType = chunk.BaseType,
                ImplementedInterfaces = chunk.ImplementedInterfaces,
                AccessModifier = chunk.AccessModifier,
                Modifiers = chunk.Modifiers
            };

            graph.AddNode(node);
        }
    }

    private static void BuildEdgesFromChunks(CodeGraph graph, List<CodeChunk> chunks)
    {
        foreach (CodeChunk chunk in chunks)
        {
            string sourceId = BuildNodeId(chunk);
            if (string.IsNullOrEmpty(sourceId)) continue;

            // Calls edges
            if (chunk.CallsOut is { Count: > 0 })
            {
                foreach (CallReference call in chunk.CallsOut)
                {
                    string? targetId = ResolveCallTarget(graph, call);
                    if (targetId == null) continue;

                    graph.AddEdge(new GraphEdge
                    {
                        SourceId = sourceId,
                        TargetId = targetId,
                        Kind = GraphEdgeKind.Calls,
                        Line = call.Line,
                        Label = call.MethodName
                    });
                }
            }

            // Field access edges
            if (chunk.FieldAccesses is { Count: > 0 })
            {
                foreach (FieldAccess access in chunk.FieldAccesses)
                {
                    string? targetId = ResolveFieldTarget(graph, access);
                    if (targetId == null) continue;

                    graph.AddEdge(new GraphEdge
                    {
                        SourceId = sourceId,
                        TargetId = targetId,
                        Kind = access.Kind == FieldAccessKind.Write ? GraphEdgeKind.FieldWrite : GraphEdgeKind.FieldRead,
                        Line = access.Line,
                        Label = access.FieldName
                    });
                }
            }

            // Inheritance edge
            if (!string.IsNullOrEmpty(chunk.BaseType))
            {
                IReadOnlyList<string> baseIds = graph.ResolveSymbol(chunk.BaseType);
                if (baseIds.Count > 0)
                {
                    graph.AddEdge(new GraphEdge
                    {
                        SourceId = sourceId,
                        TargetId = baseIds[0],
                        Kind = GraphEdgeKind.Inherits,
                        Label = chunk.BaseType
                    });
                }
            }

            // Interface implementation edges
            if (chunk.ImplementedInterfaces is { Count: > 0 })
            {
                foreach (string iface in chunk.ImplementedInterfaces)
                {
                    IReadOnlyList<string> ifaceIds = graph.ResolveSymbol(iface);
                    if (ifaceIds.Count > 0)
                    {
                        graph.AddEdge(new GraphEdge
                        {
                            SourceId = sourceId,
                            TargetId = ifaceIds[0],
                            Kind = GraphEdgeKind.Implements,
                            Label = iface
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Build a stable node ID from a chunk. Prefers QualifiedName,
    /// falls back to constructed name from namespace + parent + symbol.
    /// </summary>
    private static string BuildNodeId(CodeChunk chunk)
    {
        if (!string.IsNullOrEmpty(chunk.QualifiedName))
            return chunk.QualifiedName;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(chunk.Namespace)) parts.Add(chunk.Namespace);
        if (!string.IsNullOrEmpty(chunk.ParentSymbol)) parts.Add(chunk.ParentSymbol);
        if (!string.IsNullOrEmpty(chunk.SymbolName)) parts.Add(chunk.SymbolName);

        return parts.Count > 0 ? string.Join(".", parts) : "";
    }

    /// <summary>
    /// Resolve a call reference to a target node ID.
    /// Priority: QualifiedName > ReceiverType.MethodName > SymbolName lookup.
    /// </summary>
    private static string? ResolveCallTarget(CodeGraph graph, CallReference call)
    {
        // Best case: fully qualified name from Roslyn
        if (!string.IsNullOrEmpty(call.QualifiedName) && graph.ContainsNode(call.QualifiedName))
            return call.QualifiedName;

        // Try ReceiverType.MethodName
        if (!string.IsNullOrEmpty(call.ReceiverType))
        {
            string compound = $"{call.ReceiverType}.{call.MethodName}";
            IReadOnlyList<string> resolved = graph.ResolveSymbol(compound);
            if (resolved.Count > 0) return resolved[0];
        }

        // Fallback: bare method name
        IReadOnlyList<string> byName = graph.ResolveSymbol(call.MethodName);
        return byName.Count == 1 ? byName[0] : null; // Only use if unambiguous
    }

    /// <summary>
    /// Resolve a field access to a target node ID.
    /// </summary>
    private static string? ResolveFieldTarget(CodeGraph graph, FieldAccess access)
    {
        if (!string.IsNullOrEmpty(access.ContainingType))
        {
            string compound = $"{access.ContainingType}.{access.FieldName}";
            IReadOnlyList<string> resolved = graph.ResolveSymbol(compound);
            if (resolved.Count > 0) return resolved[0];
        }

        IReadOnlyList<string> byName = graph.ResolveSymbol(access.FieldName);
        return byName.Count == 1 ? byName[0] : null;
    }

    // ────────────────────────────────────────────────────────────────
    //  Private: Traversal helpers
    // ────────────────────────────────────────────────────────────────

    private FlowTraceResult TraceFlow(string collectionName, string symbolId, int maxDepth, bool forward)
    {
        CodeGraph graph = GetGraphOrThrow(collectionName);

        IReadOnlyList<string> resolvedIds = graph.ResolveSymbol(symbolId);
        if (resolvedIds.Count == 0)
        {
            return new FlowTraceResult
            {
                StartSymbol = symbolId,
                Direction = forward ? "forward" : "backward",
                MaxDepth = maxDepth,
                StepsByDepth = new Dictionary<int, List<FlowStep>>(),
                AllNodes = [],
                AllEdges = []
            };
        }

        string startId = resolvedIds[0];
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startId };
        var stepsByDepth = new Dictionary<int, List<FlowStep>>();
        var allNodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var allEdges = new List<GraphEdge>();

        // Add root node
        GraphNode? rootNode = graph.GetNode(startId);
        if (rootNode != null)
            allNodes[startId] = rootNode;

        // BFS
        var frontier = new List<string> { startId };

        for (int depth = 1; depth <= maxDepth && frontier.Count > 0; depth++)
        {
            var stepsAtDepth = new List<FlowStep>();
            var nextFrontier = new List<string>();

            foreach (string currentId in frontier)
            {
                IReadOnlyList<GraphEdge> edges = forward
                    ? graph.GetOutgoingEdges(currentId)
                    : graph.GetIncomingEdges(currentId);

                foreach (GraphEdge edge in edges)
                {
                    string neighborId = forward ? edge.TargetId : edge.SourceId;
                    if (!visited.Add(neighborId)) continue;

                    GraphNode? neighbor = graph.GetNode(neighborId);
                    if (neighbor == null) continue;

                    allNodes.TryAdd(neighborId, neighbor);
                    allEdges.Add(edge);

                    stepsAtDepth.Add(new FlowStep
                    {
                        Node = neighbor,
                        IncomingEdge = edge,
                        Depth = depth
                    });

                    nextFrontier.Add(neighborId);
                }
            }

            if (stepsAtDepth.Count > 0)
                stepsByDepth[depth] = stepsAtDepth;

            frontier = nextFrontier;
        }

        return new FlowTraceResult
        {
            StartSymbol = symbolId,
            Direction = forward ? "forward" : "backward",
            MaxDepth = maxDepth,
            StepsByDepth = stepsByDepth,
            AllNodes = allNodes.Values.ToList(),
            AllEdges = allEdges
        };
    }

    private static void DfsCycleDetect(
        CodeGraph graph,
        string nodeId,
        HashSet<string> visited,
        HashSet<string> onStack,
        List<string> stack,
        List<List<string>> cycles)
    {
        visited.Add(nodeId);
        onStack.Add(nodeId);
        stack.Add(nodeId);

        foreach (GraphEdge edge in graph.GetOutgoingEdges(nodeId))
        {
            if (edge.Kind != GraphEdgeKind.Calls) continue; // Only detect call cycles

            if (!visited.Contains(edge.TargetId))
            {
                DfsCycleDetect(graph, edge.TargetId, visited, onStack, stack, cycles);
            }
            else if (onStack.Contains(edge.TargetId))
            {
                // Found a cycle — extract it
                int cycleStart = stack.IndexOf(edge.TargetId);
                if (cycleStart >= 0)
                {
                    var cycle = stack.GetRange(cycleStart, stack.Count - cycleStart);
                    cycle.Add(edge.TargetId); // Close the loop
                    cycles.Add(cycle);
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        onStack.Remove(nodeId);
    }

    // ────────────────────────────────────────────────────────────────
    //  Private: Summarization helpers
    // ────────────────────────────────────────────────────────────────

    private ComponentSummary BuildComponentSummary(CodeGraph graph, string namespaceName)
    {
        IReadOnlyList<GraphNode> nodes = graph.GetNodesByNamespace(namespaceName);

        int classCount = nodes.Count(n => n.ChunkType == "class");
        int methodCount = nodes.Count(n => n.ChunkType == "method");
        int propertyCount = nodes.Count(n => n.ChunkType == "property");
        List<string> publicSymbols = nodes
            .Where(n => n.AccessModifier == "public")
            .Select(n => n.SymbolName)
            .ToList();

        // Count incoming/outgoing edges crossing namespace boundaries
        int incomingEdgeCount = 0;
        int outgoingEdgeCount = 0;
        var dependsOn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dependedOnBy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (GraphNode node in nodes)
        {
            foreach (GraphEdge edge in graph.GetOutgoingEdges(node.Id))
            {
                GraphNode? target = graph.GetNode(edge.TargetId);
                if (target?.Namespace == null) continue;
                if (string.Equals(target.Namespace, namespaceName, StringComparison.OrdinalIgnoreCase)) continue;

                outgoingEdgeCount++;
                dependsOn.Add(target.Namespace);
            }

            foreach (GraphEdge edge in graph.GetIncomingEdges(node.Id))
            {
                GraphNode? source = graph.GetNode(edge.SourceId);
                if (source?.Namespace == null) continue;
                if (string.Equals(source.Namespace, namespaceName, StringComparison.OrdinalIgnoreCase)) continue;

                incomingEdgeCount++;
                dependedOnBy.Add(source.Namespace);
            }
        }

        return new ComponentSummary
        {
            Namespace = namespaceName,
            ClassCount = classCount,
            MethodCount = methodCount,
            PropertyCount = propertyCount,
            PublicSymbols = publicSymbols,
            IncomingEdgeCount = incomingEdgeCount,
            OutgoingEdgeCount = outgoingEdgeCount,
            DependsOn = dependsOn.OrderBy(s => s).ToList(),
            DependedOnBy = dependedOnBy.OrderBy(s => s).ToList()
        };
    }

    private CodeGraph GetGraphOrThrow(string collectionName)
    {
        if (_graphs.TryGetValue(collectionName, out CodeGraph? graph))
            return graph;

        throw new InvalidOperationException(
            $"No graph has been built for collection '{collectionName}'. Call BuildGraphAsync first.");
    }
}
