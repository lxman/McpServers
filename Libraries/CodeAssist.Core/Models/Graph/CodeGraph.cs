namespace CodeAssist.Core.Models.Graph;

/// <summary>
/// In-memory directed graph representing data flow relationships between code symbols.
/// Thread-safe for concurrent reads; mutations must be externally synchronized.
/// </summary>
public sealed class CodeGraph
{
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GraphEdge>> _outgoing = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GraphEdge>> _incoming = new(StringComparer.OrdinalIgnoreCase);

    // Secondary indexes
    private readonly Dictionary<string, HashSet<string>> _fileToNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _namespaceToNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _symbolNameToNodes = new(StringComparer.OrdinalIgnoreCase);

    public int NodeCount => _nodes.Count;
    public int EdgeCount => _outgoing.Values.Sum(l => l.Count);

    // ── Node operations ─────────────────────────────────────────────

    public void AddNode(GraphNode node)
    {
        _nodes[node.Id] = node;

        // Update secondary indexes
        if (!string.IsNullOrEmpty(node.FilePath))
        {
            if (!_fileToNodes.TryGetValue(node.FilePath, out HashSet<string>? set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _fileToNodes[node.FilePath] = set;
            }
            set.Add(node.Id);
        }

        if (!string.IsNullOrEmpty(node.Namespace))
        {
            if (!_namespaceToNodes.TryGetValue(node.Namespace, out HashSet<string>? set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _namespaceToNodes[node.Namespace] = set;
            }
            set.Add(node.Id);
        }

        if (!string.IsNullOrEmpty(node.SymbolName))
        {
            if (!_symbolNameToNodes.TryGetValue(node.SymbolName, out HashSet<string>? set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _symbolNameToNodes[node.SymbolName] = set;
            }
            set.Add(node.Id);
        }
    }

    public GraphNode? GetNode(string id)
    {
        return _nodes.GetValueOrDefault(id);
    }

    public bool ContainsNode(string id)
    {
        return _nodes.ContainsKey(id);
    }

    public IEnumerable<GraphNode> GetAllNodes()
    {
        return _nodes.Values;
    }

    // ── Edge operations ─────────────────────────────────────────────

    public void AddEdge(GraphEdge edge)
    {
        if (!_outgoing.TryGetValue(edge.SourceId, out List<GraphEdge>? outList))
        {
            outList = [];
            _outgoing[edge.SourceId] = outList;
        }
        outList.Add(edge);

        if (!_incoming.TryGetValue(edge.TargetId, out List<GraphEdge>? inList))
        {
            inList = [];
            _incoming[edge.TargetId] = inList;
        }
        inList.Add(edge);
    }

    public IReadOnlyList<GraphEdge> GetOutgoingEdges(string nodeId)
    {
        return _outgoing.GetValueOrDefault(nodeId) ?? (IReadOnlyList<GraphEdge>)[];
    }

    public IReadOnlyList<GraphEdge> GetIncomingEdges(string nodeId)
    {
        return _incoming.GetValueOrDefault(nodeId) ?? (IReadOnlyList<GraphEdge>)[];
    }

    // ── Batch operations ────────────────────────────────────────────

    /// <summary>
    /// Remove all nodes (and their edges) that belong to the given file.
    /// Used for incremental updates when a file changes.
    /// </summary>
    public void RemoveNodesByFile(string filePath)
    {
        if (!_fileToNodes.TryGetValue(filePath, out HashSet<string>? nodeIds))
            return;

        foreach (string nodeId in nodeIds.ToList())
        {
            RemoveNode(nodeId);
        }

        _fileToNodes.Remove(filePath);
    }

    /// <summary>
    /// Get all nodes belonging to a specific file.
    /// </summary>
    public IReadOnlyList<GraphNode> GetNodesByFile(string filePath)
    {
        if (!_fileToNodes.TryGetValue(filePath, out HashSet<string>? nodeIds))
            return [];

        return nodeIds.Select(id => _nodes.GetValueOrDefault(id)).Where(n => n != null).ToList()!;
    }

    /// <summary>
    /// Get all nodes in a namespace.
    /// </summary>
    public IReadOnlyList<GraphNode> GetNodesByNamespace(string namespaceName)
    {
        if (!_namespaceToNodes.TryGetValue(namespaceName, out HashSet<string>? nodeIds))
            return [];

        return nodeIds.Select(id => _nodes.GetValueOrDefault(id)).Where(n => n != null).ToList()!;
    }

    /// <summary>
    /// Get all known namespaces.
    /// </summary>
    public IReadOnlyList<string> GetAllNamespaces()
    {
        return _namespaceToNodes.Keys.ToList();
    }

    /// <summary>
    /// Resolve a symbol name to all matching node IDs.
    /// Checks qualified name first (exact), then symbol name index (may be ambiguous).
    /// </summary>
    public IReadOnlyList<string> ResolveSymbol(string symbolName)
    {
        // Exact match by node ID (usually qualified name)
        if (_nodes.ContainsKey(symbolName))
            return [symbolName];

        // Lookup by symbol name index
        if (_symbolNameToNodes.TryGetValue(symbolName, out HashSet<string>? nodeIds))
            return nodeIds.ToList();

        return [];
    }

    /// <summary>
    /// Clear the entire graph.
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        _outgoing.Clear();
        _incoming.Clear();
        _fileToNodes.Clear();
        _namespaceToNodes.Clear();
        _symbolNameToNodes.Clear();
    }

    // ── Private helpers ─────────────────────────────────────────────

    private void RemoveNode(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out GraphNode? node))
            return;

        // Remove outgoing edges and their incoming references
        if (_outgoing.TryGetValue(nodeId, out List<GraphEdge>? outEdges))
        {
            foreach (GraphEdge edge in outEdges)
            {
                _incoming.GetValueOrDefault(edge.TargetId)?.RemoveAll(e => e.SourceId == nodeId);
            }
            _outgoing.Remove(nodeId);
        }

        // Remove incoming edges and their outgoing references
        if (_incoming.TryGetValue(nodeId, out List<GraphEdge>? inEdges))
        {
            foreach (GraphEdge edge in inEdges)
            {
                _outgoing.GetValueOrDefault(edge.SourceId)?.RemoveAll(e => e.TargetId == nodeId);
            }
            _incoming.Remove(nodeId);
        }

        // Remove from secondary indexes
        if (!string.IsNullOrEmpty(node.Namespace))
            _namespaceToNodes.GetValueOrDefault(node.Namespace)?.Remove(nodeId);

        if (!string.IsNullOrEmpty(node.SymbolName))
            _symbolNameToNodes.GetValueOrDefault(node.SymbolName)?.Remove(nodeId);

        _nodes.Remove(nodeId);
    }
}
