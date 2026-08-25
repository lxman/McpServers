using CodeAssist.Core.Models;
using CodeAssist.Core.Models.Graph;
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class DataFlowGraphServiceTests
{
    [Fact]
    public void BuildGraphFromChunks_MergesSplitSymbols()
    {
        CodeChunk contract = MakeChunk("ISearchBackend", "interface", "Test.ISearchBackend", 1, 10);
        CodeChunk firstPart = MakeChunk(
            "QdrantService (part 1)", "class_part1", "Test.QdrantService (part 1)", 20, 50,
            parentSymbol: "QdrantService", implementedInterfaces: ["ISearchBackend"]);
        CodeChunk secondPart = MakeChunk(
            "QdrantService (part 2)", "class_part2", "Test.QdrantService (part 2)", 45, 80,
            parentSymbol: "QdrantService", implementedInterfaces: ["ISearchBackend"]);

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks([contract, firstPart, secondPart]);

        string implementationId = Assert.Single(graph.ResolveSymbol("QdrantService"));
        Assert.Equal("Test.QdrantService", implementationId);
        GraphNode implementation = Assert.IsType<GraphNode>(graph.GetNode(implementationId));
        Assert.Equal("class", implementation.ChunkType);
        Assert.Equal(20, implementation.StartLine);
        Assert.Equal(80, implementation.EndLine);

        string contractId = Assert.Single(graph.ResolveSymbol("ISearchBackend"));
        GraphEdge edge = Assert.Single(graph.GetIncomingEdges(contractId));
        Assert.Equal(implementationId, edge.SourceId);
        Assert.Equal(GraphEdgeKind.Implements, edge.Kind);
    }

    [Fact]
    public void BuildGraphFromChunks_ResolvesCanonicalNameForSplitMethod()
    {
        CodeChunk firstPart = MakeChunk(
            "ProcessAsync (part 1)", "method_part1", "Test.Worker.ProcessAsync (part 1)", 20, 50,
            parentSymbol: "ProcessAsync");
        CodeChunk secondPart = MakeChunk(
            "ProcessAsync (part 2)", "method_part2", "Test.Worker.ProcessAsync (part 2)", 45, 80,
            parentSymbol: "ProcessAsync");

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks([firstPart, secondPart]);

        string id = Assert.Single(graph.ResolveSymbol("Test.Worker.ProcessAsync"));
        GraphNode method = Assert.IsType<GraphNode>(graph.GetNode(id));
        Assert.Equal("ProcessAsync", method.SymbolName);
        Assert.Equal("Worker", method.ParentSymbol);
        Assert.Equal("method", method.ChunkType);
    }

    [Fact]
    public void BuildGraphFromChunks_DoesNotBindAmbiguousBareCalls()
    {
        CodeChunk caller = MakeChunk(
            "RunAsync", "method", "Test.Worker.RunAsync", 1, 10,
            parentSymbol: "Worker",
            calls: [new CallReference { MethodName = "LoadAsync", Line = 5 }]);
        CodeChunk firstTarget = MakeChunk("LoadAsync", "method", "First.Loader.LoadAsync", 1, 10, "Loader", "first.cs");
        CodeChunk secondTarget = MakeChunk("LoadAsync", "method", "Second.Loader.LoadAsync", 1, 10, "Loader", "second.cs");

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks([caller, firstTarget, secondTarget]);

        Assert.Empty(graph.GetOutgoingEdges("Test.Worker.RunAsync"));
    }

    [Fact]
    public void BuildGraphFromChunks_DoesNotBindUniqueBareCallAcrossTypes()
    {
        CodeChunk caller = MakeChunk(
            "RunAsync", "method", "Test.Worker.RunAsync", 1, 10,
            parentSymbol: "Worker",
            calls: [new CallReference { MethodName = "UniquelyNamedAsync", Line = 5 }]);
        CodeChunk unrelatedTarget = MakeChunk(
            "UniquelyNamedAsync", "method", "Other.Loader.UniquelyNamedAsync", 1, 10,
            "Loader", "other.cs");

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks([caller, unrelatedTarget]);

        Assert.Empty(graph.GetOutgoingEdges("Test.Worker.RunAsync"));
    }

    [Fact]
    public void BuildGraphFromChunks_PrefersQualifiedCallTarget()
    {
        CodeChunk caller = MakeChunk(
            "RunAsync", "method", "Test.Worker.RunAsync", 1, 10,
            parentSymbol: "Worker",
            calls:
            [
                new CallReference
                {
                    MethodName = "LoadAsync",
                    QualifiedName = "First.Loader.LoadAsync",
                    Line = 5
                }
            ]);
        CodeChunk firstTarget = MakeChunk("LoadAsync", "method", "First.Loader.LoadAsync", 1, 10, "Loader", "first.cs");
        CodeChunk secondTarget = MakeChunk("LoadAsync", "method", "Second.Loader.LoadAsync", 1, 10, "Loader", "second.cs");

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks([caller, firstTarget, secondTarget]);

        GraphEdge edge = Assert.Single(graph.GetOutgoingEdges("Test.Worker.RunAsync"));
        Assert.Equal("First.Loader.LoadAsync", edge.TargetId);
    }

    [Fact]
    public void BuildGraphFromChunks_AssignsOverlappingCallToMethodOnly()
    {
        var call = new CallReference { MethodName = "SaveAsync", QualifiedName = "Test.Store.SaveAsync", Line = 20 };
        CodeChunk containingClass = MakeChunk(
            "Worker", "class", "Test.Worker", 1, 100,
            calls: [call]);
        CodeChunk method = MakeChunk(
            "RunAsync", "method", "Test.Worker.RunAsync", 10, 30,
            parentSymbol: "Worker", calls: [call]);
        CodeChunk target = MakeChunk("SaveAsync", "method", "Test.Store.SaveAsync", 1, 10, "Store", "store.cs");

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks([containingClass, method, target]);

        Assert.Empty(graph.GetOutgoingEdges("Test.Worker"));
        Assert.Single(graph.GetOutgoingEdges("Test.Worker.RunAsync"));
    }

    [Fact]
    public void BuildGraphFromChunks_DoesNotBindFieldWithoutContainingType()
    {
        CodeChunk caller = MakeChunk(
            "RunAsync", "method", "Test.Worker.RunAsync", 1, 10,
            parentSymbol: "Worker",
            fieldAccesses:
            [
                new FieldAccess { FieldName = "Key", Kind = FieldAccessKind.Read, Line = 5 }
            ]);
        CodeChunk unrelatedProperty = MakeChunk(
            "Key", "property", "Other.Options.Key", 1, 1, "Options", "options.cs");

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks([caller, unrelatedProperty]);

        Assert.Empty(graph.GetOutgoingEdges("Test.Worker.RunAsync"));
    }

    [Fact]
    public void BuildGraphFromChunks_DoesNotBindAmbiguousSameTypeCallAcrossNamespaces()
    {
        CodeChunk caller = MakeChunk(
            "RunAsync", "method", "Test.Worker.RunAsync", 1, 10,
            parentSymbol: "Worker",
            calls: [new CallReference { MethodName = "LoadAsync", Line = 5 }]);
        CodeChunk localTarget = MakeChunk(
            "LoadAsync", "method", "Test.Worker.LoadAsync", 20, 30, "Worker");
        CodeChunk unrelatedTarget = MakeChunk(
            "LoadAsync", "method", "Other.Worker.LoadAsync", 1, 10,
            "Worker", "other.cs");

        CodeGraph graph = DataFlowGraphService.BuildGraphFromChunks(
            [caller, localTarget, unrelatedTarget]);

        Assert.Empty(graph.GetOutgoingEdges("Test.Worker.RunAsync"));
    }

    [Fact]
    public void MergeFullFlowTraces_IncludesRootAndDeduplicatesSharedEdges()
    {
        GraphNode root = MakeNode("Test.Worker.RunAsync");
        GraphNode caller = MakeNode("Test.Controller.HandleAsync");
        GraphNode callee = MakeNode("Test.Store.SaveAsync");
        var callerEdge = new GraphEdge
        {
            SourceId = caller.Id, TargetId = root.Id, Kind = GraphEdgeKind.Calls
        };
        var calleeEdge = new GraphEdge
        {
            SourceId = root.Id, TargetId = callee.Id, Kind = GraphEdgeKind.Calls
        };
        FlowTraceResult forward = MakeTrace("forward", [root, callee], [calleeEdge],
            new FlowStep { Node = callee, IncomingEdge = calleeEdge, Depth = 1 });
        FlowTraceResult backward = MakeTrace("backward", [root, caller], [callerEdge],
            new FlowStep { Node = caller, IncomingEdge = callerEdge, Depth = 1 });

        FlowTraceResult merged = DataFlowGraphService.MergeFullFlowTraces(
            root.Id, 5, forward, backward);

        Assert.Equal(3, merged.AllNodes.Count);
        Assert.Contains(merged.AllNodes, node => node.Id == root.Id);
        Assert.Equal(2, merged.AllEdges.Count);
        Assert.Contains(-1, merged.StepsByDepth.Keys);
        Assert.Contains(1, merged.StepsByDepth.Keys);
    }

    private static FlowTraceResult MakeTrace(
        string direction,
        List<GraphNode> nodes,
        List<GraphEdge> edges,
        FlowStep step) => new()
    {
        StartSymbol = "Test.Worker.RunAsync",
        Direction = direction,
        MaxDepth = 5,
        StepsByDepth = new Dictionary<int, List<FlowStep>> { [1] = [step] },
        AllNodes = nodes,
        AllEdges = edges
    };

    private static GraphNode MakeNode(string id) => new()
    {
        Id = id,
        ChunkId = Guid.NewGuid(),
        SymbolName = id[(id.LastIndexOf('.') + 1)..],
        QualifiedName = id,
        Namespace = "Test",
        ChunkType = "method",
        FilePath = "test.cs"
    };

    private static CodeChunk MakeChunk(
        string symbolName,
        string chunkType,
        string qualifiedName,
        int startLine,
        int endLine,
        string? parentSymbol = null,
        string relativePath = "test.cs",
        IReadOnlyList<CallReference>? calls = null,
        IReadOnlyList<string>? implementedInterfaces = null,
        IReadOnlyList<FieldAccess>? fieldAccesses = null)
    {
        return new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = relativePath,
            RelativePath = relativePath,
            Content = symbolName,
            StartLine = startLine,
            EndLine = endLine,
            ChunkType = chunkType,
            SymbolName = symbolName,
            ParentSymbol = parentSymbol,
            CallsOut = calls,
            FieldAccesses = fieldAccesses,
            Language = "csharp",
            ContentHash = Guid.NewGuid().ToString("N"),
            Namespace = qualifiedName[..qualifiedName.IndexOf('.')],
            QualifiedName = qualifiedName,
            ImplementedInterfaces = implementedInterfaces,
            AccessModifier = "public"
        };
    }
}
