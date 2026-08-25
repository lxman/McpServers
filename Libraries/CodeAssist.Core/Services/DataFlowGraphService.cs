using System.Collections.Concurrent;
using CodeAssist.Core.Analysis;
using CodeAssist.Core.Chunking;
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
    private readonly SolutionAnalyzer _solutionAnalyzer;
    private readonly ILogger<DataFlowGraphService> _logger;
    private readonly VersionedCache<CodeGraph> _graphs = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SolutionStructure> _solutions = new(StringComparer.OrdinalIgnoreCase);

    public DataFlowGraphService(
        QdrantService qdrant,
        SolutionAnalyzer solutionAnalyzer,
        ILogger<DataFlowGraphService> logger)
    {
        _qdrant = qdrant;
        _solutionAnalyzer = solutionAnalyzer;
        _logger = logger;
        _qdrant.CollectionChanged += InvalidateCollection;
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
            while (true)
            {
                long version = _graphs.CaptureVersion(collectionName);
                _logger.LogInformation("Building data flow graph for {Collection}", collectionName);

                List<CodeChunk> chunks = await _qdrant.ScrollAllChunksAsync(collectionName, cancellationToken);

                CodeGraph graph = BuildGraphFromChunks(chunks);

                if (!_graphs.TryStore(collectionName, version, graph))
                {
                    _logger.LogInformation(
                        "Collection {Collection} changed while its graph was building; rebuilding",
                        collectionName);
                    continue;
                }

                _logger.LogInformation(
                    "Built graph for {Collection}: {Nodes} nodes, {Edges} edges",
                    collectionName, graph.NodeCount, graph.EdgeCount);

                return graph;
            }
        }
        finally
        {
            buildLock.Release();
        }
    }

    /// <summary>
    /// Analyze the solution structure for a repository.
    /// Parses .slnx and .csproj files to extract projects, references, and packages.
    /// Links namespaces from the code graph to their containing projects.
    /// </summary>
    public SolutionStructure? AnalyzeSolution(string collectionName, string repositoryPath)
    {
        string? solutionFile = SolutionAnalyzer.FindSolutionFile(repositoryPath);

        SolutionStructure? structure = solutionFile != null
            ? _solutionAnalyzer.Analyze(solutionFile)
            : _solutionAnalyzer.AnalyzeFromCsprojFiles(repositoryPath);

        if (structure == null) return null;

        // Link namespaces to projects using file paths from the code graph
        if (_graphs.TryGet(collectionName, out CodeGraph graph))
        {
            LinkNamespacesToProjects(graph, structure);
        }

        _solutions[collectionName] = structure;
        return structure;
    }

    /// <summary>
    /// Get a previously analyzed solution structure.
    /// </summary>
    public SolutionStructure? GetSolutionStructure(string collectionName)
    {
        _solutions.TryGetValue(collectionName, out SolutionStructure? structure);
        return structure;
    }

    /// <summary>
    /// Link namespaces from the code graph to their containing projects
    /// by matching file paths to project directories.
    /// </summary>
    private static void LinkNamespacesToProjects(CodeGraph graph, SolutionStructure structure)
    {
        // Build a map from directory prefix → project
        var dirToProject = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (ProjectInfo project in structure.Projects)
        {
            // Project directory is the csproj's parent directory
            // Path.GetDirectoryName on Windows converts to backslashes, so normalize back
            string projectDir = (Path.GetDirectoryName(project.RelativePath.Replace('\\', '/'))
                ?? "").Replace('\\', '/');
            if (!string.IsNullOrEmpty(projectDir))
                dirToProject[projectDir] = project;
        }

        // For each namespace in the graph, find which project its files belong to
        foreach (string ns in graph.GetAllNamespaces())
        {
            IReadOnlyList<GraphNode> nodes = graph.GetNodesByNamespace(ns);
            if (nodes.Count == 0) continue;

            // Check the first node's file path to determine the project
            GraphNode node = nodes[0];
            if (node.FilePath == null) continue;

            string filePath = node.FilePath.Replace('\\', '/');

            foreach ((string dir, ProjectInfo project) in dirToProject)
            {
                if (filePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                {
                    project.Namespaces ??= [];
                    if (!project.Namespaces.Contains(ns, StringComparer.OrdinalIgnoreCase))
                        project.Namespaces.Add(ns);
                    break;
                }
            }
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
        // Graph nodes are keyed by the chunk's relative path, which is normalized at construction.
        // A caller handing us a Windows-shaped path would remove nothing and then match nothing.
        relativePath = IndexPath.Normalize(relativePath);

        if (!_graphs.TryGet(collectionName, out CodeGraph graph))
        {
            _logger.LogDebug("No graph for {Collection}, skipping file rebuild", collectionName);
            return;
        }

        SemaphoreSlim buildLock = _buildLocks.GetOrAdd(collectionName, _ => new SemaphoreSlim(1, 1));
        await buildLock.WaitAsync(cancellationToken);

        try
        {
            // Fetch before mutating. Removing first and then failing to fetch would leave the file's
            // nodes deleted with nothing to replace them, turning a transient Qdrant error into a
            // silently incomplete graph.
            List<SearchResult> results;
            try
            {
                results = await _qdrant.SearchByFilePathAsync(collectionName, relativePath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to fetch chunks for {File} in {Collection}; leaving the graph unchanged",
                    relativePath, collectionName);
                return;
            }

            List<CodeChunk> chunks = results.Select(r => r.Chunk).ToList();

            graph.RemoveNodesByFile(relativePath);
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
        return _graphs.Get(collectionName);
    }

    private void InvalidateCollection(string collectionName)
    {
        _graphs.Invalidate(collectionName);
        _solutions.TryRemove(collectionName, out _);
        _logger.LogDebug("Invalidated graph caches for changed collection {Collection}", collectionName);
    }

    // ────────────────────────────────────────────────────────────────
    //  5b — Graph Traversal
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trace data flow forward from a symbol: what does it call, what data does it pass?
    /// </summary>
    public FlowTraceResult TraceForward(
        string collectionName,
        string symbolId,
        int maxDepth = 5,
        int maxNodes = int.MaxValue)
    {
        return TraceFlow(collectionName, symbolId, maxDepth, forward: true, maxNodes);
    }

    /// <summary>
    /// Trace data flow backward from a symbol: who calls it and with what data?
    /// </summary>
    public FlowTraceResult TraceBackward(
        string collectionName,
        string symbolId,
        int maxDepth = 5,
        int maxNodes = int.MaxValue)
    {
        return TraceFlow(collectionName, symbolId, maxDepth, forward: false, maxNodes);
    }

    /// <summary>
    /// Trace data flow in both directions from a symbol.
    /// </summary>
    public FlowTraceResult TraceFullFlow(
        string collectionName,
        string symbolId,
        int maxDepth = 5,
        int maxNodes = int.MaxValue)
    {
        FlowTraceResult forward = TraceFlow(collectionName, symbolId, maxDepth, forward: true, maxNodes);
        FlowTraceResult backward = TraceFlow(collectionName, symbolId, maxDepth, forward: false, maxNodes);

        return MergeFullFlowTraces(symbolId, maxDepth, forward, backward);
    }

    internal static FlowTraceResult MergeFullFlowTraces(
        string symbolId,
        int maxDepth,
        FlowTraceResult forward,
        FlowTraceResult backward)
    {

        // Merge results
        var allNodes = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var allEdges = new List<GraphEdge>();
        var stepsByDepth = new Dictionary<int, List<FlowStep>>();

        // Each one-way trace includes the root in AllNodes. Seed from those complete node sets
        // rather than only from steps, which exclude the root by design.
        foreach (GraphNode node in backward.AllNodes.Concat(forward.AllNodes))
            allNodes.TryAdd(node.Id, node);

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
            AllEdges = allEdges.Distinct().ToList(),
            TraversalTruncated = forward.TraversalTruncated || backward.TraversalTruncated
        };
    }

    /// <summary>
    /// Analyze the impact of changing a symbol: what else is affected?
    /// Traces backward (who calls this?) and forward through inheritance/implementation.
    /// </summary>
    public ImpactResult AnalyzeImpact(
        string collectionName,
        string symbolId,
        int maxDepth = 5,
        int maxNodes = int.MaxValue)
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
        bool traversalTruncated = false;
        maxNodes = Math.Max(1, maxNodes);

        // BFS backward through callers + forward through overrides/implementations
        var queue = new Queue<(string nodeId, int distance)>();
        queue.Enqueue((startId, 0));

        while (queue.Count > 0)
        {
            (string currentId, int distance) = queue.Dequeue();

            // Callers (backward through Calls edges)
            foreach (GraphEdge inEdge in graph.GetIncomingEdges(currentId))
            {
                if (visited.Contains(inEdge.SourceId)) continue;
                if (directlyAffected.Count + transitivelyAffected.Count >= maxNodes)
                {
                    traversalTruncated = true;
                    queue.Clear();
                    break;
                }

                visited.Add(inEdge.SourceId);
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
            TransitivelyAffected = transitivelyAffected,
            TraversalTruncated = traversalTruncated
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

    internal static CodeGraph BuildGraphFromChunks(List<CodeChunk> chunks)
    {
        var graph = new CodeGraph();
        BuildNodesFromChunks(graph, chunks);
        BuildEdgesFromChunks(graph, chunks);
        return graph;
    }

    private static void BuildNodesFromChunks(CodeGraph graph, List<CodeChunk> chunks)
    {
        IEnumerable<IGrouping<string, CodeChunk>> symbolGroups = chunks
            .Select(chunk => (Id: BuildNodeId(chunk), Chunk: chunk))
            .Where(item => !string.IsNullOrEmpty(item.Id))
            .GroupBy(item => item.Id, item => item.Chunk, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, CodeChunk> group in symbolGroups)
        {
            List<CodeChunk> symbolChunks = group.ToList();
            CodeChunk representative = symbolChunks
                .OrderBy(chunk => chunk.StartLine)
                .ThenBy(chunk => chunk.EndLine)
                .First();

            var node = new GraphNode
            {
                Id = group.Key,
                ChunkId = representative.Id,
                SymbolName = GetCanonicalSymbolName(representative) ?? "",
                QualifiedName = group.Key,
                Namespace = representative.Namespace,
                ChunkType = GetCanonicalChunkType(representative.ChunkType),
                FilePath = representative.RelativePath,
                StartLine = symbolChunks.Min(chunk => chunk.StartLine),
                EndLine = symbolChunks.Max(chunk => chunk.EndLine),
                ParentSymbol = GetCanonicalParentSymbol(representative),
                ReturnType = symbolChunks.Select(chunk => chunk.ReturnType).FirstOrDefault(value => !string.IsNullOrEmpty(value)),
                BaseType = symbolChunks.Select(chunk => chunk.BaseType).FirstOrDefault(value => !string.IsNullOrEmpty(value)),
                ImplementedInterfaces = symbolChunks
                    .SelectMany(chunk => chunk.ImplementedInterfaces ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AccessModifier = symbolChunks.Select(chunk => chunk.AccessModifier).FirstOrDefault(value => !string.IsNullOrEmpty(value)),
                Modifiers = symbolChunks
                    .SelectMany(chunk => chunk.Modifiers ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            graph.AddNode(node);
        }
    }

    private static void BuildEdgesFromChunks(CodeGraph graph, List<CodeChunk> chunks)
    {
        // Build constructor parameter map: (className, paramName) → paramType
        // This lets us resolve receiver expressions like "qdrantService" to "QdrantService"
        // when the receiver is a constructor-injected dependency.
        Dictionary<(string ClassName, string ParamName), string> ctorParamMap = BuildConstructorParamMap(chunks);

        // Build DI service map: interfaceName → implementationName
        // from Program.cs / ServiceCollectionExtensions.cs files.
        Dictionary<string, string> diMap = BuildDiServiceMap(chunks);

        Dictionary<string, List<CodeChunk>> callableChunksByFile = chunks
            .Where(chunk => IsCallableChunk(chunk.ChunkType))
            .GroupBy(chunk => chunk.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (CodeChunk chunk in chunks)
        {
            string sourceId = BuildNodeId(chunk);
            if (string.IsNullOrEmpty(sourceId)) continue;

            // Determine the enclosing class for receiver type lookups
            string? enclosingClass = GetEnclosingTypeName(chunk);

            // Calls edges
            if (chunk.CallsOut is { Count: > 0 })
            {
                foreach (CallReference call in chunk.CallsOut)
                {
                    if (!IsCallableChunk(chunk.ChunkType)
                        && HasNarrowerCallableOwner(callableChunksByFile, chunk, call.Line))
                    {
                        continue;
                    }

                    IReadOnlyList<string> targetIds = ResolveCallTargets(
                        graph, call, enclosingClass, ctorParamMap, diMap);
                    foreach (string targetId in targetIds)
                    {
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
            }

            // Field access edges
            if (chunk.FieldAccesses is { Count: > 0 })
            {
                foreach (FieldAccess access in chunk.FieldAccesses)
                {
                    if (!IsCallableChunk(chunk.ChunkType)
                        && HasNarrowerCallableOwner(callableChunksByFile, chunk, access.Line))
                    {
                        continue;
                    }

                    IReadOnlyList<string> targetIds = ResolveFieldTargets(graph, access);
                    foreach (string targetId in targetIds)
                    {
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
            }

            // Inheritance edge
            if (!string.IsNullOrEmpty(chunk.BaseType))
            {
                IReadOnlyList<string> baseIds = graph.ResolveSymbol(chunk.BaseType);
                if (baseIds.Count == 1)
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
                    if (ifaceIds.Count == 1)
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
    /// Build a map from (className, parameterName) → parameterType by scanning
    /// class-level and constructor chunks for their parameters.  This enables
    /// resolving constructor-injected receivers like "qdrantService" → "QdrantService".
    /// </summary>
    private static Dictionary<(string, string), string> BuildConstructorParamMap(List<CodeChunk> chunks)
    {
        var map = new Dictionary<(string, string), string>(CtorParamKeyComparer.Instance);

        foreach (CodeChunk chunk in chunks)
        {
            if (chunk.Parameters is not { Count: > 0 }) continue;

            // For class chunks with primary constructor params, use SymbolName as the class
            // For constructor chunks, use ParentSymbol as the class
            string? className = chunk.ChunkType switch
            {
                "class" or "record" or "struct" => chunk.SymbolName,
                "constructor" => chunk.ParentSymbol,
                _ when chunk.ChunkType.StartsWith("class_part", StringComparison.Ordinal) => chunk.ParentSymbol,
                _ => null
            };

            if (string.IsNullOrEmpty(className)) continue;

            foreach (ParameterInfo param in chunk.Parameters)
            {
                if (!string.IsNullOrEmpty(param.Type))
                {
                    // Strip generic args for lookup: IOptions<Foo> → IOptions
                    string normalizedType = param.Type;
                    int angleBracket = normalizedType.IndexOf('<');
                    if (angleBracket >= 0)
                        normalizedType = normalizedType[..angleBracket];

                    map.TryAdd((className, param.Name), normalizedType);
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Build a DI service map (interfaceName → implementationName) by parsing
    /// DI registration patterns from Program.cs and ServiceCollectionExtensions.cs files.
    /// </summary>
    private static Dictionary<string, string> BuildDiServiceMap(List<CodeChunk> chunks)
    {
        var combined = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (CodeChunk chunk in chunks)
        {
            if (string.IsNullOrEmpty(chunk.RelativePath)) continue;
            if (!DiRegistrationParser.IsDiRegistrationFile(chunk.RelativePath)) continue;
            if (string.IsNullOrEmpty(chunk.Content)) continue;

            foreach (KeyValuePair<string, string> kvp in DiRegistrationParser.ExtractServiceMappings(chunk.Content))
            {
                combined.TryAdd(kvp.Key, kvp.Value);
            }
        }

        return combined;
    }

    /// <summary>
    /// Build a stable node ID from a chunk. Prefers QualifiedName,
    /// falls back to constructed name from namespace + parent + symbol.
    /// </summary>
    private static string BuildNodeId(CodeChunk chunk)
    {
        if (!string.IsNullOrEmpty(chunk.QualifiedName))
            return RemovePartSuffix(chunk.QualifiedName);

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(chunk.Namespace)) parts.Add(chunk.Namespace);
        string? parent = GetCanonicalParentSymbol(chunk);
        string? symbol = GetCanonicalSymbolName(chunk);
        if (!string.IsNullOrEmpty(parent)) parts.Add(parent);
        if (!string.IsNullOrEmpty(symbol)) parts.Add(symbol);

        return parts.Count > 0 ? string.Join(".", parts) : "";
    }

    private static string? GetCanonicalSymbolName(CodeChunk chunk)
    {
        if (string.IsNullOrEmpty(chunk.SymbolName)) return null;
        return RemovePartSuffix(chunk.SymbolName);
    }

    private static string GetCanonicalChunkType(string chunkType)
    {
        int partIndex = chunkType.LastIndexOf("_part", StringComparison.Ordinal);
        if (partIndex < 0) return chunkType;

        ReadOnlySpan<char> suffix = chunkType.AsSpan(partIndex + 5);
        return !suffix.IsEmpty && suffix.IndexOfAnyExceptInRange('0', '9') < 0
            ? chunkType[..partIndex]
            : chunkType;
    }

    private static string RemovePartSuffix(string value)
    {
        const string marker = " (part ";
        int markerIndex = value.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 || value[^1] != ')') return value;

        ReadOnlySpan<char> partNumber = value.AsSpan(markerIndex + marker.Length, value.Length - markerIndex - marker.Length - 1);
        return !partNumber.IsEmpty && partNumber.IndexOfAnyExceptInRange('0', '9') < 0
            ? value[..markerIndex]
            : value;
    }

    private static string? GetCanonicalParentSymbol(CodeChunk chunk)
    {
        string canonicalType = GetCanonicalChunkType(chunk.ChunkType);
        if (canonicalType is "class" or "record" or "struct" or "interface" or "enum")
            return chunk.ChunkType == canonicalType ? chunk.ParentSymbol : null;

        if (chunk.ChunkType == canonicalType)
            return chunk.ParentSymbol;

        string canonicalQualifiedName = !string.IsNullOrEmpty(chunk.QualifiedName)
            ? RemovePartSuffix(chunk.QualifiedName)
            : "";
        int lastDot = canonicalQualifiedName.LastIndexOf('.');
        if (lastDot < 0) return null;

        string containingName = canonicalQualifiedName[..lastDot];
        int containingDot = containingName.LastIndexOf('.');
        return containingDot >= 0 ? containingName[(containingDot + 1)..] : containingName;
    }

    private static string? GetEnclosingTypeName(CodeChunk chunk)
    {
        string canonicalType = GetCanonicalChunkType(chunk.ChunkType);
        return canonicalType is "class" or "record" or "struct" or "interface" or "enum"
            ? GetCanonicalSymbolName(chunk)
            : GetCanonicalParentSymbol(chunk);
    }

    private static bool IsCallableChunk(string chunkType)
    {
        string canonicalType = GetCanonicalChunkType(chunkType);
        return canonicalType is "method" or "function" or "constructor" or "property";
    }

    private static bool HasNarrowerCallableOwner(
        Dictionary<string, List<CodeChunk>> callableChunksByFile,
        CodeChunk source,
        int line)
    {
        return callableChunksByFile.TryGetValue(source.RelativePath, out List<CodeChunk>? candidates)
            && candidates.Any(candidate =>
                candidate.StartLine <= line
                && candidate.EndLine >= line
                && candidate.EndLine - candidate.StartLine < source.EndLine - source.StartLine);
    }

    /// <summary>
    /// Resolve a call reference to target node IDs.
    /// Priority: QualifiedName > ReceiverType.MethodName > ConstructorParam resolution
    ///           > DI interface resolution > SymbolName lookup.
    /// Ambiguous bare names remain unresolved rather than creating false edges.
    /// </summary>
    private static IReadOnlyList<string> ResolveCallTargets(
        CodeGraph graph,
        CallReference call,
        string? enclosingClass,
        Dictionary<(string, string), string> ctorParamMap,
        Dictionary<string, string> diMap)
    {
        // Best case: fully qualified name from Roslyn
        if (!string.IsNullOrEmpty(call.QualifiedName) && graph.ContainsNode(call.QualifiedName))
            return [call.QualifiedName];

        // Try ReceiverType.MethodName
        if (!string.IsNullOrEmpty(call.ReceiverType))
        {
            string compound = $"{call.ReceiverType}.{call.MethodName}";
            IReadOnlyList<string> resolved = graph.ResolveSymbol(compound);
            if (resolved.Count == 1) return resolved;
        }

        // Unqualified calls usually target another member on the enclosing type.
        if (string.IsNullOrEmpty(call.ReceiverExpression) && !string.IsNullOrEmpty(enclosingClass))
        {
            IReadOnlyList<string> resolved = graph.ResolveSymbol($"{enclosingClass}.{call.MethodName}");
            if (resolved.Count == 1) return resolved;
        }

        // Try constructor parameter type resolution:
        // If the receiver expression matches a constructor parameter, use its type.
        if (!string.IsNullOrEmpty(call.ReceiverExpression) && !string.IsNullOrEmpty(enclosingClass))
        {
            // The receiver expression may be "qdrantService" or "_qdrantService" or "this._qdrantService"
            string receiver = call.ReceiverExpression;
            if (receiver.StartsWith("this.", StringComparison.Ordinal))
                receiver = receiver[5..];
            string bareReceiver = receiver.TrimStart('_');

            if (ctorParamMap.TryGetValue((enclosingClass, bareReceiver), out string? paramType)
                || ctorParamMap.TryGetValue((enclosingClass, receiver), out paramType))
            {
                // Direct type resolution: paramType.MethodName
                string compound = $"{paramType}.{call.MethodName}";
                IReadOnlyList<string> resolved = graph.ResolveSymbol(compound);
                if (resolved.Count == 1) return resolved;

                // If the param type is an interface, check the DI map for the concrete impl
                if (diMap.TryGetValue(paramType, out string? implType))
                {
                    compound = $"{implType}.{call.MethodName}";
                    resolved = graph.ResolveSymbol(compound);
                    if (resolved.Count == 1) return resolved;
                }
            }
        }

        // Repository-wide bare-name matching is not type resolution. Even a currently unique
        // method name can belong to an unrelated type, so leave it unresolved.
        return [];
    }

    /// <summary>
    /// Resolve a field access to target node IDs.
    /// </summary>
    private static IReadOnlyList<string> ResolveFieldTargets(CodeGraph graph, FieldAccess access)
    {
        if (!string.IsNullOrEmpty(access.ContainingType))
        {
            string compound = $"{access.ContainingType}.{access.FieldName}";
            IReadOnlyList<string> resolved = graph.ResolveSymbol(compound);
            if (resolved.Count == 1) return resolved;
        }

        // A field/property name without a resolved containing type is not a stable identity.
        // Even a repository-wide unique name can belong to an unrelated type.
        return [];
    }

    // ────────────────────────────────────────────────────────────────
    //  Private: Traversal helpers
    // ────────────────────────────────────────────────────────────────

    private FlowTraceResult TraceFlow(
        string collectionName,
        string symbolId,
        int maxDepth,
        bool forward,
        int maxNodes)
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
        bool traversalTruncated = false;
        maxNodes = Math.Max(1, maxNodes);

        // Add root node
        GraphNode? rootNode = graph.GetNode(startId);
        if (rootNode != null)
            allNodes[startId] = rootNode;

        // BFS
        var frontier = new List<string> { startId };

        for (int depth = 1;
             depth <= maxDepth && frontier.Count > 0 && !traversalTruncated;
             depth++)
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
                    if (visited.Contains(neighborId)) continue;
                    if (allNodes.Count >= maxNodes)
                    {
                        traversalTruncated = true;
                        break;
                    }

                    visited.Add(neighborId);

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
            AllEdges = allEdges,
            TraversalTruncated = traversalTruncated
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
        if (_graphs.TryGet(collectionName, out CodeGraph graph))
            return graph;

        throw new InvalidOperationException(
            $"No graph has been built for collection '{collectionName}'. Call BuildGraphAsync first.");
    }

    /// <summary>
    /// Case-insensitive equality comparer for (className, paramName) tuple keys.
    /// </summary>
    private sealed class CtorParamKeyComparer : IEqualityComparer<(string, string)>
    {
        public static readonly CtorParamKeyComparer Instance = new();

        public bool Equals((string, string) x, (string, string) y)
        {
            return string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string, string) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
        }
    }
}
