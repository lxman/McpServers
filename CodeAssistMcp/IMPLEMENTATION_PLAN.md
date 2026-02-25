# CodeAssistMcp — Data Flow Graph Implementation Plan

## Goal

Give the AI the ability to trace the complete flow of data through an entire project. This enables an AI-first IDE where the user sees a high-level data flow overview and drills down to specific code sections.

---

## Architecture: Two-Tier Analysis

### Tier 1 — Tree-sitter (All Languages)

Fast, syntax-level extraction. Works for every supported language. Extracts structure, signatures, and call relationships from the AST.

### Tier 2 — Language-Specific Semantic Analysis (Starting with Roslyn for C#)

Deep semantic analysis where tooling exists. Resolves types, qualifies names, traces data flow through assignments, maps interface-to-implementation, disambiguates overloads.

Future Tier 2 candidates: TypeScript compiler API, gopls, rust-analyzer.

---

## Indexing Philosophy

When the AI begins working on a codebase, CodeAssist starts parsing the entire project and sets up file watchers to monitor changes. The initial indexing takes time — a few minutes for a medium-sized codebase, potentially 5-10 minutes for large ones. This is acceptable and consistent with how modern IDEs perform background indexing when opening a new workspace. Performance optimization comes later.

### Parallel Analysis Pipeline

Tree-sitter and Roslyn operate on different inputs and produce complementary data. They do not depend on each other and run in parallel per file:

```
File discovered
    ├── Tree-sitter pass (fast, syntax-level) ──┐
    │                                            ├── Merge → Embed → Store in Qdrant
    └── Roslyn pass (slower, semantic) ──────────┘
```

- **Tree-sitter** works from raw source text + grammar definitions. Produces: structure, signatures, call references, inheritance (syntax-level).
- **Roslyn** works from the MSBuild workspace/compilation. Produces: qualified names, resolved types, parameter types, data flow, interface-to-implementation mappings.
- **Merge** combines both into a single enriched `CodeChunk`. Roslyn data supplements/overrides tree-sitter data where both provide the same field (e.g., Roslyn provides the resolved type for a call that tree-sitter only has the bare name for).
- **Non-C# files** skip the Roslyn branch entirely — tree-sitter output goes straight to merge.

### Incremental Updates

After initial indexing, the file watcher detects changes and re-indexes only affected files. The same parallel pipeline runs for changed files, updating the hot cache (L1), Qdrant (L2), and the in-memory graph.

---

## Phase 1: Enrich the CodeChunk Model

Extend `CodeChunk` to carry the richer data both tiers will produce.

### New Fields

```csharp
// Signature information
public IReadOnlyList<ParameterInfo>? Parameters { get; init; }
public string? ReturnType { get; init; }

// Type relationships
public string? BaseType { get; init; }                          // extends
public IReadOnlyList<string>? ImplementedInterfaces { get; init; } // implements

// Access and modifiers
public string? AccessModifier { get; init; }                    // public, private, protected, internal
public IReadOnlyList<string>? Modifiers { get; init; }          // static, abstract, virtual, async, sealed

// Attributes / decorators
public IReadOnlyList<string>? Attributes { get; init; }

// Enhanced call information (replaces bare CallsOut)
public IReadOnlyList<CallReference>? CallsOut { get; init; }

// Field and property access
public IReadOnlyList<FieldAccess>? FieldAccesses { get; init; }

// Namespace / module context
public string? Namespace { get; init; }
public string? QualifiedName { get; init; }                     // namespace.class.method
```

### Supporting Types

```csharp
public sealed record ParameterInfo
{
    public required string Name { get; init; }
    public string? Type { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsOut { get; init; }
    public bool IsRef { get; init; }
    public bool IsParams { get; init; }
}

public sealed record CallReference
{
    public required string MethodName { get; init; }
    public string? ReceiverType { get; init; }       // the type the method is called on
    public string? ReceiverExpression { get; init; }  // e.g., "service", "this", "base"
    public string? QualifiedName { get; init; }       // fully resolved (Tier 2 only)
    public int Line { get; init; }
}

public sealed record FieldAccess
{
    public required string FieldName { get; init; }
    public string? ContainingType { get; init; }
    public FieldAccessKind Kind { get; init; }        // Read, Write, ReadWrite
    public int Line { get; init; }
}

public enum FieldAccessKind { Read, Write, ReadWrite }
```

---

## Phase 2: Enhanced Tree-sitter Extraction (Tier 1)

Enhance `TreeSitterChunker` to extract richer syntax-level data from the AST.

### 2a — Parameter and Return Type Extraction

- Walk `parameter_list` / `formal_parameters` children of method/function nodes
- Extract parameter names, type annotations where present
- Extract return type from method signature nodes
- Language-specific node types needed per grammar

### 2b — Inheritance and Interface Extraction

- Walk `base_list` (C#), `extends`/`implements` clauses (Java/TS), etc.
- Extract base class and implemented interface names

### 2c — Namespace and Qualified Name Construction

- Walk up the AST to find enclosing namespace/module declarations
- Build qualified names: `Namespace.Class.Method`

### 2d — Enhanced Call Extraction

- Current: extracts bare method name from call expressions
- Enhanced: also capture the receiver expression and line number
- e.g., `service.ProcessPayment(order)` → `CallReference { MethodName: "ProcessPayment", ReceiverExpression: "service", Line: 42 }`

### 2e — Field Access Tracking

- Identify assignment expressions where fields/properties are read or written
- Track `member_access_expression` nodes in assignment contexts
- Distinguish reads vs writes based on position in assignment

### 2f — Access Modifiers and Attributes

- Extract modifier keywords from declaration nodes
- Extract attribute/decorator nodes preceding declarations

### 2g — Parent Symbol Wiring

- The infrastructure exists but `ParentSymbol` is set to `null` for normal chunks
- Wire it up: when a method is inside a class, set `ParentSymbol` to the class name

### Priority Order

2g (quick win) → 2a → 2c → 2d → 2b → 2e → 2f

---

## Phase 3: Semantic Analyzer Interface + Roslyn Implementation

### 3a — Define `ISemanticAnalyzer` Interface

All language-specific semantic analyzers implement the same contract. This lets the indexing pipeline treat them uniformly and makes adding new languages a matter of plugging in a new implementation.

```csharp
public interface ISemanticAnalyzer
{
    /// Languages this analyzer handles (e.g., "csharp", "typescript", "rust")
    IReadOnlySet<string> SupportedLanguages { get; }

    /// Initialize the analyzer for a project (load workspace, compilation, etc.)
    /// This may be slow — runs once at startup, in parallel with tree-sitter indexing.
    Task InitializeAsync(string projectPath, CancellationToken cancellationToken);

    /// Enrich tree-sitter chunks with semantic data for a single file.
    /// Called after tree-sitter produces initial chunks. Returns enriched copies.
    Task<IReadOnlyList<CodeChunk>> EnrichChunksAsync(
        string filePath,
        IReadOnlyList<CodeChunk> treeSitterChunks,
        CancellationToken cancellationToken);

    /// Extract DI / IoC container registrations (interface → implementation mappings).
    /// Returns empty if the language/framework doesn't use DI or the analyzer doesn't support it.
    Task<IReadOnlyList<DependencyMapping>> ExtractDependencyMappingsAsync(
        CancellationToken cancellationToken);

    /// Handle a file change incrementally (update internal compilation state).
    Task OnFileChangedAsync(string filePath, CancellationToken cancellationToken);

    /// Extract HTTP endpoints this project exposes or consumes.
    /// Servers return endpoints they serve; clients return endpoints they call.
    /// The graph links matching endpoints across tiers.
    Task<IReadOnlyList<HttpEndpointInfo>> ExtractHttpEndpointsAsync(
        CancellationToken cancellationToken);

    /// Whether the analyzer has finished initialization and is ready to enrich.
    bool IsReady { get; }
}

public sealed record HttpEndpointInfo
{
    public required string HttpMethod { get; init; }       // GET, POST, PUT, DELETE, etc.
    public required string RouteTemplate { get; init; }    // "/api/orders", "/api/orders/{id}"
    public required HttpEndpointRole Role { get; init; }   // Server or Client
    public required string SymbolName { get; init; }       // method/function that defines or calls it
    public string? QualifiedName { get; init; }            // fully qualified symbol
    public string? FilePath { get; init; }
    public int Line { get; init; }
}

public enum HttpEndpointRole { Server, Client }

public sealed record DependencyMapping
{
    public required string InterfaceType { get; init; }    // fully qualified
    public required string ConcreteType { get; init; }     // fully qualified
    public string? Lifetime { get; init; }                 // Scoped, Singleton, Transient
}
```

### 3b — Analyzer Registry

A simple registry that maps languages to their analyzers. The indexing pipeline queries it per file:

```csharp
public class SemanticAnalyzerRegistry
{
    // Register analyzers at startup via DI
    // Lookup: given a language string, return the analyzer (or null for tree-sitter-only)
    ISemanticAnalyzer? GetAnalyzer(string language);
}
```

### 3c — Roslyn Implementation (`RoslynSemanticAnalyzer : ISemanticAnalyzer`)

The first and most complete implementation. Absorbs existing CSharpAnalyzer.Core capabilities.

**`InitializeAsync`:**
- Load MSBuild workspace for the solution/project
- Build the compilation (restores NuGet references, resolves types)
- Runs in parallel with tree-sitter indexing at startup

**`EnrichChunksAsync`:**
- For each tree-sitter chunk, use Roslyn's `SemanticModel` to add:
  - Fully qualified names for symbols and call targets
  - Resolved parameter types and return types
  - Receiver types on call references (not just expression text)
  - Overload disambiguation
  - Virtual/interface dispatch identification
  - Access modifiers and modifiers from symbol metadata
  - Type hierarchy (base type, implemented interfaces)
  - Data flow analysis (`SemanticModel.AnalyzeDataFlow()`) for variables read/written, parameter flow, return value flow, closure captures

**`ExtractDependencyMappingsAsync`:**
- Parse `Program.cs` / `Startup.cs` for DI registrations
- Standard patterns (Microsoft.Extensions.DependencyInjection):
  - `services.AddScoped<IFoo, Foo>()` — direct mapping
  - `services.AddSingleton<IFoo>(sp => new Foo(...))` — factory lambdas
  - `services.AddTransient<IFoo, Foo>()` — same pattern
  - Extension methods that wrap these (e.g., `services.AddHttpClient<IFoo, Foo>()`)
- Result feeds into the graph as `Interface → ConcreteType` edges
- Limitations: convention-based registration (Scrutor), reflection-based, and third-party containers (Autofac modules) are opaque initially. Can extend later with container-specific parsers.

**`OnFileChangedAsync`:**
- Roslyn's `Solution`/`Compilation` model is immutable and snapshot-based. On file change:
  1. Call `solution.WithDocumentText(docId, newText)` to get a new `Solution` snapshot
  2. Roslyn internally shares all unchanged state — only the changed file's syntax tree is reparsed
  3. Request the `SemanticModel` for the changed file — Roslyn incrementally recomputes only what's affected
  4. Re-enrich that file's chunks with the updated semantic data
- **Method body changes:** Only that file's semantic model updates. Fast.
- **Type signature changes** (e.g., adding a method to an interface): Can cascade to dependent files. Roslyn handles this lazily — dependents are not eagerly recompiled. They get refreshed when next queried or when the file watcher triggers their re-indexing.
- This is the same mechanism VS and OmniSharp use for live editing. No full recompilation needed.

### 3d — Future Implementations

| Language | Analyzer | Semantic Tooling | DI Patterns |
|----------|----------|-----------------|-------------|
| C# | `RoslynSemanticAnalyzer` | Roslyn | Microsoft.Extensions.DI |
| TypeScript | `TypeScriptSemanticAnalyzer` | TypeScript Compiler API | Angular DI, NestJS |
| Rust | `RustSemanticAnalyzer` | rust-analyzer | N/A |
| Go | `GoSemanticAnalyzer` | gopls | wire, fx |
| Java | `JavaSemanticAnalyzer` | Eclipse JDT / Java compiler API | Spring DI |

Each implements `ISemanticAnalyzer`. The indexing pipeline doesn't change — it just gets richer data as analyzers are added.

### 3e — Integration Pipeline

- Tree-sitter runs immediately on all files (produces initial chunks)
- Semantic analyzers initialize in parallel (e.g., Roslyn loads the workspace)
- Once an analyzer is ready (`IsReady == true`), it enriches chunks going forward
- Initial tree-sitter-only chunks get backfilled with semantic data once the analyzer is warm
- Non-analyzed languages retain tree-sitter-only data — still useful, just less resolved
- Merge results: Roslyn data overwrites/supplements tree-sitter data for C# files
- Non-C# files retain tree-sitter-only data

### Design Decision: Where Does Roslyn Run?

Absorb CSharpAnalyzerMcp into CodeAssistMcp. The existing Roslyn services and models from CSharpAnalyzer.Core get pulled into CodeAssist.Core, and the CSharpAnalyzerMcp MCP tools get folded into CodeAssistMcp's tool surface. One project, one process, direct access to Roslyn during indexing with no MCP round-trips.

---

## Phase 4: Qdrant Storage and Indexing Enhancements

### 4a — Updated Payload Schema

Store all new fields in the Qdrant point payload:

- `parameters` (JSON array)
- `return_type` (keyword)
- `base_type` (keyword)
- `implemented_interfaces` (list of keywords)
- `access_modifier` (keyword)
- `modifiers` (list of keywords)
- `attributes` (list of keywords)
- `calls_out` (JSON array of CallReference objects)
- `field_accesses` (JSON array)
- `namespace` (keyword)
- `qualified_name` (keyword)

### 4b — New Payload Indexes

- `qualified_name` — fast lookup by fully qualified symbol
- `base_type` — find all subclasses of a type
- `implemented_interfaces` — find all implementations of an interface
- `namespace` — filter by namespace
- `return_type` — find methods returning a specific type
- `access_modifier` — filter by visibility

### 4c — Graph Query Methods

New methods on `QdrantService`:

- `FindImplementationsOfAsync(interfaceName)` — all chunks implementing an interface
- `FindSubclassesOfAsync(baseType)` — all chunks extending a type
- `FindMethodsByReturnTypeAsync(typeName)` — methods returning a specific type
- `FindFieldAccessesAsync(fieldName, kind?)` — who reads/writes a field
- `TraceCallChainAsync(symbolName, depth)` — multi-hop caller/callee traversal

---

## Phase 5: Data Flow Graph Service

A new service that builds and traverses the complete data flow graph.

### 5a — In-Memory Graph Representation

- Build a directed graph from Qdrant data
- Nodes: symbols (methods, properties, fields, classes)
- Edges: calls, field access, inheritance, interface implementation
- Edge metadata: line number, access kind, call arguments

### 5b — Graph Traversal Operations

- **Forward flow:** Given a method, what does it call and what data does it pass?
- **Backward flow:** Given a method, who calls it and with what data?
- **Full trace:** Follow a value from entry point through all transformations
- **Impact analysis:** What is affected if this method/field changes?
- **Cycle detection:** Find circular dependencies
- **Reachability:** Is this code reachable from a given entry point?

### 5c — Graph Caching and Invalidation

- Cache computed graphs in memory
- Invalidate on file change events (from FileWatcherService)
- Lazy rebuild: only recompute affected subgraphs

### 5d — Summarization for UI

- Generate high-level flow summaries (entry points → processing → outputs)
- Identify key data pathways through the system
- Group related symbols into logical components/layers
- Produce drill-down hierarchies: System → Component → Class → Method → Line

---

## Phase 6: New MCP Tools

Expose graph capabilities to the AI:

- `trace_data_flow(repoName, startSymbol, direction, maxDepth)` — trace forward/backward from a symbol
- `get_type_hierarchy(repoName, typeName)` — full inheritance/implementation tree
- `find_implementations(repoName, interfaceName)` — concrete implementations
- `impact_analysis(repoName, symbolName)` — what's affected by a change
- `get_system_overview(repoName)` — high-level component/data flow summary
- `get_component_detail(repoName, componentName)` — drill into a component
- `find_entry_points(repoName)` — identify public API / entry points
- `detect_cycles(repoName)` — circular dependency detection

---

## Phase 7: UI Foundation (Future)

The graph data enables a drill-down UI:

1. **System Overview** — high-level data flow between components/namespaces
2. **Component View** — classes and their relationships within a component
3. **Class View** — methods, properties, inheritance, implementations
4. **Method View** — call graph, parameter flow, field access
5. **Code View** — actual source with inline annotations

Each level links to the next, driven by graph queries against Qdrant.

---

## Resolved Decisions

1. **Embedding model:** Will change as the indexed data evolves. Specifics TBD — depends on what enriched chunks look like and what retrieval patterns the graph queries need.
2. **Graph storage:** In-memory graph (QuikGraph or custom adjacency list) for all traversal queries. Qdrant remains the durable store for enriched chunks + embeddings. The graph is derived data built from Qdrant on startup, kept current via FileWatcher events. If cold start rebuild becomes slow on large repos, add a lightweight persistence snapshot (SQLite, LiteDB, or similar) so the graph can be restored without a full Qdrant scroll. Specific persistence tech TBD — other design decisions downstream may influence this choice.
3. **Roslyn workspace loading:** Load eagerly at startup, in parallel with tree-sitter indexing. Tree-sitter starts producing chunks immediately while Roslyn spins up. Initial tree-sitter-only chunks get backfilled once Roslyn is ready.
4. **Incremental Roslyn analysis:** Per-file via `solution.WithDocumentText()`. No full recompilation needed.
5. **External packages:** Treat NuGet/npm packages as opaque boundary nodes. Can revisit later.

---

## Open Questions

1. **Workspace scope and cross-tier linking:** A workspace can span multiple projects, multiple languages, and potentially multiple repos (e.g., an Angular frontend + .NET API backend). The graph should be scoped per workspace, not per project or per language. This enables cross-tier data flow tracing — e.g., an Angular `HttpClient.get('/api/orders')` links to a .NET `[HttpGet("api/orders")]` controller action. This requires:
   - A cross-tier edge type in the graph (HTTP endpoint matching)
   - Extracting HTTP call targets from frontend code (Angular `HttpClient`, `fetch`, Axios, etc.)
   - Extracting route templates from backend code (ASP.NET `[Route]`/`[HttpGet]` attributes, Express routes, etc.)
   - Matching on URL patterns to create the edge
   - Both sides are discoverable through their respective analyzers — this is where the `ISemanticAnalyzer` interface pays off. Each analyzer can expose an `ExtractHttpEndpoints()` or similar contract.
   - Design: `ExtractHttpEndpointsAsync()` on `ISemanticAnalyzer` for now. Each analyzer reports endpoints it serves (Server) or consumes (Client). The graph construction phase matches them by route template + HTTP method. Can evolve to automatic extraction later.

2. **Graph node identity:** Qualified name alone breaks with overloads (`Process(int)` vs `Process(string)`) and partial classes. Need a stable identity scheme that handles these cases.

3. **Async/events/delegates:** These create implicit data flow not visible as direct call expressions. `event += Handler`, `Task.ContinueWith(...)`, `Channel<T>` producer/consumer patterns. How deep do we go initially? Suggestion: start with direct calls only, flag events/delegates as "implicit edges" for a future pass.

4. **What gets embedded?** The enriched chunk has raw code plus metadata. Do we embed raw code, code + structured metadata, or a generated summary? Affects retrieval quality.

5. **Error handling during indexing:** When Roslyn can't compile a file (syntax error, missing ref) but tree-sitter parses it fine — store tree-sitter-only data? Mark as partially enriched?

6. **UI tech stack:** Phase 7 doesn't specify technology. Web (Blazor, React)? Desktop (Avalonia, MAUI)? Affects how graph data is shaped for consumption.

---

## Implementation Order Summary

| Phase | Scope | Depends On |
|-------|-------|------------|
| 1     | CodeChunk model + supporting types | — |
| 2     | Enhanced tree-sitter extraction | Phase 1 |
| 3     | Roslyn semantic overlay (C#) | Phase 1, Phase 2 |
| 4     | Qdrant schema + graph queries | Phase 1 |
| 5     | Data flow graph service | Phase 4 |
| 6     | New MCP tools | Phase 5 |
| 7     | UI foundation | Phase 6 |

Phases 2, 3, and 4 can be partially parallelized once Phase 1 is complete.
