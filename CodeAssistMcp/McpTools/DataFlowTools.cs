using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using CodeAssist.Core.Models;
using CodeAssist.Core.Models.Graph;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CodeAssistMcp.McpTools;

/// <summary>
/// MCP tools for data flow graph analysis.
/// Exposes graph construction, traversal, impact analysis, and summarization.
/// </summary>
[McpServerToolType]
public class DataFlowTools(
    RepositoryIndexer indexer,
    DataFlowGraphService graphService,
    ILogger<DataFlowTools> logger)
{
    /// <summary>
    /// Ensures the graph is built for a collection, building it on first access.
    /// </summary>
    private async Task<CodeGraph> EnsureGraphAsync(string collectionName, CancellationToken cancellationToken)
    {
        CodeGraph? graph = graphService.GetGraph(collectionName);
        if (graph != null) return graph;

        logger.LogInformation("Graph not yet built for {Collection}, building now...", collectionName);
        return await graphService.BuildGraphAsync(collectionName, cancellationToken);
    }

    /// <summary>
    /// Resolves a repository name to its IndexState, returning a JSON error string if not found.
    /// </summary>
    private async Task<(IndexState? state, string? error)> ResolveRepository(string repositoryName)
    {
        IndexState? state = await indexer.GetIndexStateAsync(repositoryName);
        if (state != null) return (state, null);

        string error = JsonSerializer.Serialize(new
        {
            success = false,
            error = $"No index found for repository '{repositoryName}'. Use index_repository to create one first."
        }, SerializerOptions.JsonOptionsIndented);

        return (null, error);
    }

    [McpServerTool, DisplayName("trace_data_flow")]
    [Description("Trace data flow forward or backward from a symbol. Forward shows what a method calls and what data it passes. Backward shows who calls it and with what data. Use direction='both' for a complete bidirectional trace. The startSymbol can be a qualified name (e.g., 'Namespace.Class.Method'), a class.method name, or a bare symbol name.")]
    public async Task<string> TraceDataFlow(
        string repositoryName,
        string startSymbol,
        string direction = "forward",
        int maxDepth = 5)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Tracing data flow {Direction} from {Symbol} in {Repository}",
                direction, startSymbol, repositoryName);

            await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            FlowTraceResult result = direction.ToLowerInvariant() switch
            {
                "forward" => graphService.TraceForward(state.CollectionName, startSymbol, maxDepth),
                "backward" => graphService.TraceBackward(state.CollectionName, startSymbol, maxDepth),
                "both" => graphService.TraceFullFlow(state.CollectionName, startSymbol, maxDepth),
                _ => throw new ArgumentException($"Invalid direction '{direction}'. Use 'forward', 'backward', or 'both'.")
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                startSymbol = result.StartSymbol,
                direction = result.Direction,
                maxDepth = result.MaxDepth,
                totalNodes = result.AllNodes.Count,
                totalEdges = result.AllEdges.Count,
                stepsByDepth = result.StepsByDepth.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(s => new
                    {
                        symbol = s.Node.SymbolName,
                        qualifiedName = s.Node.QualifiedName,
                        chunkType = s.Node.ChunkType,
                        filePath = s.Node.FilePath,
                        startLine = s.Node.StartLine,
                        endLine = s.Node.EndLine,
                        edgeKind = s.IncomingEdge?.Kind.ToString(),
                        edgeLabel = s.IncomingEdge?.Label,
                        depth = s.Depth
                    }).ToList()),
                edges = result.AllEdges.Select(FormatEdge).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error tracing data flow from {Symbol} in {Repository}", startSymbol, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_type_hierarchy")]
    [Description("Get the full inheritance and implementation hierarchy for a type. Shows base classes (upward) and subclasses/implementors (downward). Useful for understanding class hierarchies and interface implementations.")]
    public async Task<string> GetTypeHierarchy(
        string repositoryName,
        string typeName)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Getting type hierarchy for {Type} in {Repository}", typeName, repositoryName);

            CodeGraph graph = await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            IReadOnlyList<string> resolvedIds = graph.ResolveSymbol(typeName);
            if (resolvedIds.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    repositoryName,
                    typeName,
                    found = false,
                    message = $"No type found matching '{typeName}'."
                }, SerializerOptions.JsonOptionsIndented);
            }

            string typeId = resolvedIds[0];
            GraphNode? typeNode = graph.GetNode(typeId);

            // Walk upward: base classes
            var baseChain = new List<object>();
            var currentId = typeId;
            var visitedUp = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { typeId };
            while (true)
            {
                var inheritsEdges = graph.GetOutgoingEdges(currentId)
                    .Where(e => e.Kind == GraphEdgeKind.Inherits)
                    .ToList();

                if (inheritsEdges.Count == 0) break;

                string baseId = inheritsEdges[0].TargetId;
                if (!visitedUp.Add(baseId)) break;

                GraphNode? baseNode = graph.GetNode(baseId);
                if (baseNode == null) break;

                baseChain.Add(FormatNodeSummary(baseNode));
                currentId = baseId;
            }

            // Interfaces this type implements
            var implementedInterfaces = graph.GetOutgoingEdges(typeId)
                .Where(e => e.Kind == GraphEdgeKind.Implements)
                .Select(e => graph.GetNode(e.TargetId))
                .Where(n => n != null)
                .Select(n => FormatNodeSummary(n!))
                .ToList();

            // Walk downward: subclasses (types that have an Inherits edge targeting this type)
            var subclasses = graph.GetIncomingEdges(typeId)
                .Where(e => e.Kind == GraphEdgeKind.Inherits)
                .Select(e => graph.GetNode(e.SourceId))
                .Where(n => n != null)
                .Select(n => FormatNodeSummary(n!))
                .ToList();

            // Implementors (types that have an Implements edge targeting this type)
            var implementors = graph.GetIncomingEdges(typeId)
                .Where(e => e.Kind == GraphEdgeKind.Implements)
                .Select(e => graph.GetNode(e.SourceId))
                .Where(n => n != null)
                .Select(n => FormatNodeSummary(n!))
                .ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                typeName,
                found = true,
                type = typeNode != null ? FormatNodeSummary(typeNode) : null,
                baseClasses = baseChain,
                implementedInterfaces,
                subclasses,
                implementors
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting type hierarchy for {Type} in {Repository}", typeName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("find_implementations")]
    [Description("Find all concrete implementations of an interface or abstract class. Returns the types that implement the given interface or extend the given abstract class, with file locations.")]
    public async Task<string> FindImplementations(
        string repositoryName,
        string interfaceName)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Finding implementations of {Interface} in {Repository}", interfaceName, repositoryName);

            CodeGraph graph = await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            IReadOnlyList<string> resolvedIds = graph.ResolveSymbol(interfaceName);
            if (resolvedIds.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    repositoryName,
                    interfaceName,
                    found = false,
                    implementations = Array.Empty<object>(),
                    message = $"No type found matching '{interfaceName}'."
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Collect implementors and subclasses across all resolved IDs
            var implementations = new List<object>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string ifaceId in resolvedIds)
            {
                // Direct implementors (Implements edges)
                foreach (GraphEdge edge in graph.GetIncomingEdges(ifaceId))
                {
                    if (edge.Kind != GraphEdgeKind.Implements) continue;
                    if (!seen.Add(edge.SourceId)) continue;

                    GraphNode? node = graph.GetNode(edge.SourceId);
                    if (node == null) continue;

                    implementations.Add(new
                    {
                        symbol = node.SymbolName,
                        qualifiedName = node.QualifiedName,
                        filePath = node.FilePath,
                        startLine = node.StartLine,
                        endLine = node.EndLine,
                        relationship = "implements"
                    });
                }

                // Subclasses (Inherits edges)
                foreach (GraphEdge edge in graph.GetIncomingEdges(ifaceId))
                {
                    if (edge.Kind != GraphEdgeKind.Inherits) continue;
                    if (!seen.Add(edge.SourceId)) continue;

                    GraphNode? node = graph.GetNode(edge.SourceId);
                    if (node == null) continue;

                    implementations.Add(new
                    {
                        symbol = node.SymbolName,
                        qualifiedName = node.QualifiedName,
                        filePath = node.FilePath,
                        startLine = node.StartLine,
                        endLine = node.EndLine,
                        relationship = "extends"
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                interfaceName,
                found = true,
                count = implementations.Count,
                implementations
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding implementations of {Interface} in {Repository}", interfaceName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("impact_analysis")]
    [Description("Analyze the impact of changing a symbol. Shows what other code would be directly and transitively affected if the given method, class, or property changes. Useful before refactoring to understand blast radius.")]
    public async Task<string> ImpactAnalysis(
        string repositoryName,
        string symbolName,
        int maxDepth = 5)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Analyzing impact of {Symbol} in {Repository}", symbolName, repositoryName);

            await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            ImpactResult result = graphService.AnalyzeImpact(state.CollectionName, symbolName, maxDepth);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                sourceSymbol = result.SourceSymbol,
                totalAffected = result.TotalAffectedCount,
                directlyAffected = result.DirectlyAffected.Select(n => new
                {
                    symbol = n.Node.SymbolName,
                    qualifiedName = n.Node.QualifiedName,
                    chunkType = n.Node.ChunkType,
                    filePath = n.Node.FilePath,
                    startLine = n.Node.StartLine,
                    endLine = n.Node.EndLine,
                    relationship = n.Relationship.ToString(),
                    distance = n.Distance
                }).ToList(),
                transitivelyAffected = result.TransitivelyAffected.Select(n => new
                {
                    symbol = n.Node.SymbolName,
                    qualifiedName = n.Node.QualifiedName,
                    chunkType = n.Node.ChunkType,
                    filePath = n.Node.FilePath,
                    startLine = n.Node.StartLine,
                    endLine = n.Node.EndLine,
                    relationship = n.Relationship.ToString(),
                    distance = n.Distance
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing impact of {Symbol} in {Repository}", symbolName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_system_overview")]
    [Description("Get a high-level overview of the system's architecture. Shows components (namespaces), their sizes, dependencies between them, cross-component edges, and entry points. Great for understanding the overall structure of a codebase.")]
    public async Task<string> GetSystemOverview(string repositoryName)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Getting system overview for {Repository}", repositoryName);

            await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            SystemOverview overview = graphService.GetSystemOverview(state.CollectionName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                totalNodes = overview.TotalNodes,
                totalEdges = overview.TotalEdges,
                componentCount = overview.Components.Count,
                components = overview.Components.Select(c => new
                {
                    @namespace = c.Namespace,
                    classCount = c.ClassCount,
                    methodCount = c.MethodCount,
                    propertyCount = c.PropertyCount,
                    publicSymbolCount = c.PublicSymbols.Count,
                    incomingEdges = c.IncomingEdgeCount,
                    outgoingEdges = c.OutgoingEdgeCount,
                    dependsOn = c.DependsOn,
                    dependedOnBy = c.DependedOnBy
                }).ToList(),
                crossComponentEdgeCount = overview.CrossComponentEdges.Count,
                entryPoints = overview.EntryPoints.Select(FormatNodeSummary).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting system overview for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_component_detail")]
    [Description("Get detailed information about a specific component (namespace). Shows class count, method count, public symbols, and dependency relationships with other components.")]
    public async Task<string> GetComponentDetail(
        string repositoryName,
        string componentName)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Getting component detail for {Component} in {Repository}", componentName, repositoryName);

            await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            ComponentSummary component = graphService.GetComponentDetail(state.CollectionName, componentName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                component = new
                {
                    @namespace = component.Namespace,
                    classCount = component.ClassCount,
                    methodCount = component.MethodCount,
                    propertyCount = component.PropertyCount,
                    publicSymbols = component.PublicSymbols,
                    incomingEdges = component.IncomingEdgeCount,
                    outgoingEdges = component.OutgoingEdgeCount,
                    dependsOn = component.DependsOn,
                    dependedOnBy = component.DependedOnBy
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting component detail for {Component} in {Repository}", componentName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("find_entry_points")]
    [Description("Find entry points in the codebase — public methods and classes with no incoming call edges. These are typically API controllers, event handlers, Main methods, or other top-level entry points into the system.")]
    public async Task<string> FindEntryPoints(string repositoryName)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Finding entry points in {Repository}", repositoryName);

            await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            List<GraphNode> entryPoints = graphService.FindEntryPoints(state.CollectionName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                count = entryPoints.Count,
                entryPoints = entryPoints.Select(n => new
                {
                    symbol = n.SymbolName,
                    qualifiedName = n.QualifiedName,
                    chunkType = n.ChunkType,
                    filePath = n.FilePath,
                    startLine = n.StartLine,
                    endLine = n.EndLine,
                    @namespace = n.Namespace,
                    accessModifier = n.AccessModifier
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding entry points in {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("detect_cycles")]
    [Description("Detect circular dependencies in the call graph. Returns all cycles found, where each cycle is the sequence of symbols forming the loop. Useful for identifying problematic circular dependencies that should be refactored.")]
    public async Task<string> DetectCycles(string repositoryName)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName);
            if (state == null) return error!;

            logger.LogDebug("Detecting cycles in {Repository}", repositoryName);

            await EnsureGraphAsync(state.CollectionName, CancellationToken.None);

            List<List<string>> cycles = graphService.DetectCycles(state.CollectionName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                cycleCount = cycles.Count,
                cycles = cycles.Select((cycle, index) => new
                {
                    cycleNumber = index + 1,
                    length = cycle.Count - 1, // Last element repeats the first
                    symbols = cycle
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error detecting cycles in {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    // ── Formatting helpers ──────────────────────────────────────────

    private static object FormatNodeSummary(GraphNode node) => new
    {
        symbol = node.SymbolName,
        qualifiedName = node.QualifiedName,
        chunkType = node.ChunkType,
        filePath = node.FilePath,
        startLine = node.StartLine,
        endLine = node.EndLine,
        @namespace = node.Namespace,
        accessModifier = node.AccessModifier
    };

    private static object FormatEdge(GraphEdge edge) => new
    {
        source = edge.SourceId,
        target = edge.TargetId,
        kind = edge.Kind.ToString(),
        line = edge.Line,
        label = edge.Label
    };
}
