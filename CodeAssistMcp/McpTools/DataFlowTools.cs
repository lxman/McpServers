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
    private async Task<(IndexState? state, string? error)> ResolveRepository(
        string repositoryName,
        CancellationToken cancellationToken)
    {
        IndexState? state = await indexer.GetIndexStateAsync(repositoryName, cancellationToken);
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
        int maxDepth = 5,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Tracing data flow {Direction} from {Symbol} in {Repository}",
                direction, startSymbol, repositoryName);

            CodeGraph graph = await EnsureGraphAsync(state.CollectionName, cancellationToken);

            IReadOnlyList<string> resolvedIds = graph.ResolveSymbol(startSymbol);
            if (resolvedIds.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    repositoryName,
                    startSymbol,
                    found = false,
                    message = $"No symbol found matching '{startSymbol}'."
                }, SerializerOptions.JsonOptionsIndented);
            }

            if (resolvedIds.Count > 1)
                return SerializeAmbiguousSymbol(repositoryName, startSymbol, resolvedIds, graph);

            int depthLimit = Math.Clamp(maxDepth, 1, 10);
            int resultLimit = ClampLimit(limit, 500);
            FlowTraceResult result = direction.ToLowerInvariant() switch
            {
                "forward" => graphService.TraceForward(
                    state.CollectionName, startSymbol, depthLimit, resultLimit),
                "backward" => graphService.TraceBackward(
                    state.CollectionName, startSymbol, depthLimit, resultLimit),
                "both" => graphService.TraceFullFlow(
                    state.CollectionName, startSymbol, depthLimit, resultLimit),
                _ => throw new ArgumentException($"Invalid direction '{direction}'. Use 'forward', 'backward', or 'both'.")
            };
            GraphNode? root = graph.GetNode(resolvedIds[0]);
            var returnedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root != null)
                returnedNodeIds.Add(root.Id);

            var selectedSteps = new List<FlowStep>();
            IEnumerable<FlowStep> orderedSteps = result.StepsByDepth
                .SelectMany(kvp => kvp.Value)
                .OrderBy(step => Math.Abs(step.Depth))
                .ThenBy(step => step.Depth);
            foreach (FlowStep step in orderedSteps)
            {
                if (returnedNodeIds.Count >= resultLimit) break;
                if (!returnedNodeIds.Add(step.Node.Id)) continue;
                selectedSteps.Add(step);
            }

            var limitedSteps = selectedSteps
                .GroupBy(step => step.Depth)
                .ToDictionary(group => group.Key, group => group.ToList());
            List<GraphEdge> limitedEdges = result.AllEdges
                .Where(edge => returnedNodeIds.Contains(edge.SourceId)
                    && returnedNodeIds.Contains(edge.TargetId))
                .Distinct()
                .ToList();
            int returnedNodes = returnedNodeIds.Count;

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                found = true,
                startSymbol = result.StartSymbol,
                start = root == null ? null : FormatNodeSummary(root),
                direction = result.Direction,
                maxDepth = result.MaxDepth,
                totalNodes = result.AllNodes.Count,
                totalEdges = result.AllEdges.Count,
                returnedNodes,
                returnedEdges = limitedEdges.Count,
                truncated = result.TraversalTruncated
                    || result.AllNodes.Count > returnedNodes
                    || result.AllEdges.Count > limitedEdges.Count,
                stepsByDepth = limitedSteps.ToDictionary(
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
                edges = limitedEdges.Select(FormatEdge).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        string typeName,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Getting type hierarchy for {Type} in {Repository}", typeName, repositoryName);

            CodeGraph graph = await EnsureGraphAsync(state.CollectionName, cancellationToken);

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

            if (resolvedIds.Count > 1)
                return SerializeAmbiguousSymbol(repositoryName, typeName, resolvedIds, graph);

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

            int resultLimit = ClampLimit(limit, 500);
            int totalRelationships = baseChain.Count + implementedInterfaces.Count
                + subclasses.Count + implementors.Count;
            List<object> returnedBaseClasses = baseChain.Take(resultLimit).ToList();
            int remaining = resultLimit - returnedBaseClasses.Count;
            List<object> returnedInterfaces = implementedInterfaces.Take(remaining).ToList();
            remaining -= returnedInterfaces.Count;
            List<object> returnedSubclasses = subclasses.Take(remaining).ToList();
            remaining -= returnedSubclasses.Count;
            List<object> returnedImplementors = implementors.Take(remaining).ToList();
            int returned = returnedBaseClasses.Count + returnedInterfaces.Count
                + returnedSubclasses.Count + returnedImplementors.Count;

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                typeName,
                found = true,
                type = typeNode != null ? FormatNodeSummary(typeNode) : null,
                relationshipCount = totalRelationships,
                returned,
                truncated = totalRelationships > returned,
                baseClasses = returnedBaseClasses,
                implementedInterfaces = returnedInterfaces,
                subclasses = returnedSubclasses,
                implementors = returnedImplementors
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        string interfaceName,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Finding implementations of {Interface} in {Repository}", interfaceName, repositoryName);

            CodeGraph graph = await EnsureGraphAsync(state.CollectionName, cancellationToken);

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

            if (resolvedIds.Count > 1)
                return SerializeAmbiguousSymbol(repositoryName, interfaceName, resolvedIds, graph);

            // Collect implementors and subclasses for the resolved type.
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

            int resultLimit = ClampLimit(limit, 500);
            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                interfaceName,
                found = true,
                count = implementations.Count,
                returned = Math.Min(implementations.Count, resultLimit),
                truncated = implementations.Count > resultLimit,
                implementations = implementations.Take(resultLimit)
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        int maxDepth = 5,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Analyzing impact of {Symbol} in {Repository}", symbolName, repositoryName);

            CodeGraph graph = await EnsureGraphAsync(state.CollectionName, cancellationToken);

            IReadOnlyList<string> resolvedIds = graph.ResolveSymbol(symbolName);
            if (resolvedIds.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    repositoryName,
                    symbolName,
                    found = false,
                    message = $"No symbol found matching '{symbolName}'."
                }, SerializerOptions.JsonOptionsIndented);
            }

            if (resolvedIds.Count > 1)
                return SerializeAmbiguousSymbol(repositoryName, symbolName, resolvedIds, graph);

            int resultLimit = ClampLimit(limit, 500);
            ImpactResult result = graphService.AnalyzeImpact(
                state.CollectionName, symbolName, Math.Clamp(maxDepth, 1, 10), resultLimit);
            List<ImpactedNode> direct = result.DirectlyAffected.Take(resultLimit).ToList();
            int remaining = resultLimit - direct.Count;
            List<ImpactedNode> transitive = result.TransitivelyAffected.Take(remaining).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                found = true,
                sourceSymbol = result.SourceSymbol,
                source = FormatNodeSummary(graph.GetNode(resolvedIds[0])!),
                totalAffected = result.TotalAffectedCount,
                returned = direct.Count + transitive.Count,
                truncated = result.TraversalTruncated
                    || result.TotalAffectedCount > direct.Count + transitive.Count,
                directlyAffected = direct.Select(n => new
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
                transitivelyAffected = transitive.Select(n => new
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing impact of {Symbol} in {Repository}", symbolName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_system_overview")]
    [Description("Get a high-level overview of the system's architecture. Shows components (namespaces), their sizes, dependencies between them, cross-component edges, and entry points. Great for understanding the overall structure of a codebase.")]
    public async Task<string> GetSystemOverview(
        string repositoryName,
        int maxComponents = 10,
        int maxEntryPoints = 10,
        bool includeTests = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Getting system overview for {Repository}", repositoryName);

            await EnsureGraphAsync(state.CollectionName, cancellationToken);

            SystemOverview overview = graphService.GetSystemOverview(state.CollectionName);
            int componentLimit = ClampLimit(maxComponents, 100);
            int entryPointLimit = ClampLimit(maxEntryPoints, 200);
            List<ComponentSummary> components = overview.Components
                .Where(component => includeTests || !IsTestNamespace(component.Namespace))
                .ToList();
            List<GraphNode> entryPoints = overview.EntryPoints
                .Where(node => includeTests || !IsTestNode(node))
                .ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                totalNodes = overview.TotalNodes,
                totalEdges = overview.TotalEdges,
                componentCount = overview.Components.Count,
                eligibleComponentCount = components.Count,
                componentsReturned = Math.Min(components.Count, componentLimit),
                components = components.Take(componentLimit).Select(c => new
                {
                    @namespace = c.Namespace,
                    classCount = c.ClassCount,
                    methodCount = c.MethodCount,
                    propertyCount = c.PropertyCount,
                    publicSymbolCount = c.PublicSymbols.Count,
                    incomingEdges = c.IncomingEdgeCount,
                    outgoingEdges = c.OutgoingEdgeCount,
                    dependsOnCount = c.DependsOn.Count,
                    dependsOn = c.DependsOn.Take(10),
                    dependedOnByCount = c.DependedOnBy.Count,
                    dependedOnBy = c.DependedOnBy.Take(10)
                }).ToList(),
                crossComponentEdgeCount = overview.CrossComponentEdges.Count,
                entryPointCount = overview.EntryPoints.Count,
                eligibleEntryPointCount = entryPoints.Count,
                entryPointsReturned = Math.Min(entryPoints.Count, entryPointLimit),
                entryPoints = entryPoints.Take(entryPointLimit).Select(FormatNodeSummary).ToList(),
                truncated = components.Count > componentLimit || entryPoints.Count > entryPointLimit
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        string componentName,
        int maxPublicSymbols = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Getting component detail for {Component} in {Repository}", componentName, repositoryName);

            await EnsureGraphAsync(state.CollectionName, cancellationToken);

            ComponentSummary component = graphService.GetComponentDetail(state.CollectionName, componentName);
            int symbolLimit = ClampLimit(maxPublicSymbols, 200);

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
                    publicSymbolCount = component.PublicSymbols.Count,
                    publicSymbols = component.PublicSymbols.Take(symbolLimit),
                    incomingEdges = component.IncomingEdgeCount,
                    outgoingEdges = component.OutgoingEdgeCount,
                    dependsOnCount = component.DependsOn.Count,
                    dependsOn = component.DependsOn.Take(50),
                    dependedOnByCount = component.DependedOnBy.Count,
                    dependedOnBy = component.DependedOnBy.Take(50),
                    truncated = component.PublicSymbols.Count > symbolLimit
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting component detail for {Component} in {Repository}", componentName, repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("find_entry_points")]
    [Description("Find entry points in the codebase — public methods and classes with no incoming call edges. These are typically API controllers, event handlers, Main methods, or other top-level entry points into the system.")]
    public async Task<string> FindEntryPoints(
        string repositoryName,
        int limit = 50,
        string? namespaceFilter = null,
        bool includeTests = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Finding entry points in {Repository}", repositoryName);

            await EnsureGraphAsync(state.CollectionName, cancellationToken);

            List<GraphNode> entryPoints = graphService.FindEntryPoints(state.CollectionName);
            IEnumerable<GraphNode> filtered = entryPoints.Where(node => includeTests || !IsTestNode(node));
            if (!string.IsNullOrWhiteSpace(namespaceFilter))
            {
                filtered = filtered.Where(node =>
                    node.Namespace?.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase) == true);
            }

            List<GraphNode> matching = filtered.ToList();
            int resultLimit = ClampLimit(limit, 200);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                namespaceFilter,
                count = matching.Count,
                returned = Math.Min(matching.Count, resultLimit),
                truncated = matching.Count > resultLimit,
                entryPoints = matching.Take(resultLimit).Select(n => new
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding entry points in {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("detect_cycles")]
    [Description("Detect circular dependencies in the call graph. Returns all cycles found, where each cycle is the sequence of symbols forming the loop. Useful for identifying problematic circular dependencies that should be refactored.")]
    public async Task<string> DetectCycles(
        string repositoryName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Detecting cycles in {Repository}", repositoryName);

            await EnsureGraphAsync(state.CollectionName, cancellationToken);

            List<List<string>> cycles = graphService.DetectCycles(state.CollectionName);
            int resultLimit = ClampLimit(limit, 200);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                cycleCount = cycles.Count,
                returned = Math.Min(cycles.Count, resultLimit),
                truncated = cycles.Count > resultLimit,
                cycles = cycles.Take(resultLimit).Select((cycle, index) => new
                {
                    cycleNumber = index + 1,
                    length = cycle.Count - 1, // Last element repeats the first
                    symbols = cycle
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error detecting cycles in {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_solution_structure")]
    [Description("Get the project-level structure of a solution. Shows all projects, their references (project and NuGet package), target frameworks, solution folder groupings, and which namespaces belong to each project. Requires the repository to be indexed first.")]
    public async Task<string> GetSolutionStructure(
        string repositoryName,
        int maxProjects = 50,
        bool includePackageReferences = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (IndexState? state, string? error) = await ResolveRepository(repositoryName, cancellationToken);
            if (state == null) return error!;

            logger.LogDebug("Getting solution structure for {Repository}", repositoryName);

            // Ensure graph is built so namespace linking works
            await EnsureGraphAsync(state.CollectionName, cancellationToken);

            // Check cache first, then analyze
            SolutionStructure? structure = graphService.GetSolutionStructure(state.CollectionName)
                ?? graphService.AnalyzeSolution(state.CollectionName, state.RootPath);

            if (structure == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    repositoryName,
                    found = false,
                    message = "No .slnx/.sln solution file or .csproj projects found in the repository."
                }, SerializerOptions.JsonOptionsIndented);
            }

            int projectLimit = ClampLimit(maxProjects, 200);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositoryName,
                found = true,
                solution = new
                {
                    name = structure.Name,
                    filePath = structure.FilePath,
                    projectCount = structure.Projects.Count,
                    projectsReturned = Math.Min(structure.Projects.Count, projectLimit),
                    folderCount = structure.Folders.Count,
                    projects = structure.Projects.Take(projectLimit).Select(p => new
                    {
                        name = p.Name,
                        relativePath = p.RelativePath,
                        solutionFolder = p.SolutionFolder,
                        targetFramework = p.TargetFramework,
                        outputType = p.OutputType ?? "Library",
                        projectReferences = p.ProjectReferences,
                        packageReferenceCount = p.PackageReferences.Count,
                        packageReferences = includePackageReferences
                            ? p.PackageReferences.Take(50).Select(pkg => new
                            {
                                name = pkg.Name,
                                version = pkg.Version
                            }).ToList()
                            : null,
                        namespaces = (p.Namespaces ?? []).Take(50)
                    }).ToList(),
                    folders = structure.Folders.Select(f => new
                    {
                        name = f.Name,
                        projects = f.ProjectNames
                    }).ToList(),
                    truncated = structure.Projects.Count > projectLimit
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting solution structure for {Repository}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    // ── Formatting helpers ──────────────────────────────────────────

    private static int ClampLimit(int value, int maximum) => Math.Clamp(value, 1, maximum);

    private static bool IsTestNode(GraphNode node) =>
        IsTestNamespace(node.Namespace)
        || node.FilePath is { Length: > 0 } filePath && IndexPath.IsTestPath(filePath);

    private static bool IsTestNamespace(string? value) => value?
        .Split('.', StringSplitOptions.RemoveEmptyEntries)
        .Any(segment => segment.Equals("Test", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Tests", StringComparison.OrdinalIgnoreCase)) == true;

    private static string SerializeAmbiguousSymbol(
        string repositoryName,
        string requestedSymbol,
        IReadOnlyList<string> resolvedIds,
        CodeGraph graph)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            repositoryName,
            requestedSymbol,
            ambiguous = true,
            message = "The symbol name is ambiguous. Retry with one of the qualified names.",
            matches = resolvedIds.Take(50)
                .Select(graph.GetNode)
                .Where(node => node != null)
                .Select(node => FormatNodeSummary(node!))
                .ToList()
        }, SerializerOptions.JsonOptionsIndented);
    }

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
