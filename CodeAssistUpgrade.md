# CodeAssist: Voice Profile + Dependency Graph

## Context

CodeAssist is an MCP server that indexes codebases into a vector DB (Qdrant) for semantic search. Two features are needed: (1) serve the user's voice profile so any connected agent framework can match their writing style, and (2) extract call-graph edges during TreeSitter chunking so search results can expand to include callers and callees.

## Feature 1: Voice Profile Tool

Small feature. New tool, one config property, one line in Program.cs.

### Files to modify

- `Libraries\CodeAssist.Core\Configuration\CodeAssistOptions.cs` -- add `VoiceProfilePath` property (string?, default null)
- `CodeAssistMcp\Program.cs` -- add `.WithTools<PersonalContextTools>()`
- `CodeAssistMcp\appsettings.json` -- add `VoiceProfilePath` key

### Files to create

- `CodeAssistMcp\McpTools\PersonalContextTools.cs` -- `[McpServerToolType]` class with `get_voice_profile` tool. Reads file from configured path, returns content as JSON. Follows existing tool pattern (primary constructor DI, DisplayName/Description attributes, JSON serialization with SerializerOptions.JsonOptionsIndented, try/catch returning error JSON).

## Feature 2: Dependency Graph

Larger feature. Touches model, chunking, storage, search, and tools.

### Step 1: Add CallsOut to CodeChunk

**File:** `Libraries\CodeAssist.Core\Models\CodeChunk.cs`

Add after ParentSymbol (line 51):

```csharp
public IReadOnlyList<string>? CallsOut { get; init; }
```

Nullable so existing chunks (DefaultChunker, file-level) stay null. No downstream breakage.

### Step 2: Call extraction in TreeSitterChunker

**File:** `Libraries\CodeAssist.Core\Chunking\TreeSitterChunker.cs`

Add a static dictionary mapping languages to their call expression node types:

- csharp: `invocation_expression`
- python: `call`
- javascript/typescript: `call_expression`
- go: `call_expression`
- rust: `call_expression`, `method_call_expression`
- java: `method_invocation`
- c/cpp: `call_expression`
- ruby: `method_call`, `call`
- php: `function_call_expression`, `method_call_expression`

Add three private static methods:

- `ExtractCallsOut(Node node, string language)` -- entry point, returns `List<string>`
- `WalkForCalls(Node node, HashSet<string> callTypes, HashSet<string> calls)` -- recursive subtree walk using `node.ChildCount` and `node.Child(i)` (same API pattern as existing code)
- `ExtractCallName(Node callNode)` -- extracts the short function/method name from a call node. For `service.ProcessPayment()` returns `ProcessPayment`. Walks the first child for member access patterns, extracts the rightmost identifier.

Uses `HashSet<string>` to deduplicate (if ProcessPayment called 3 times, store once).

Wire into `ExtractChunksWithQuery` at line 273 (normal chunk path): call `ExtractCallsOut(node, language)` and set `CallsOut = callsOut.Count > 0 ? callsOut : null` on the CodeChunk.

Also wire into `ExtractTopLevelNodes` at line 322 -- same pattern, call extraction on each top-level node.

`SplitLargeChunk` and `CreateFileChunk` set `CallsOut = null` (no AST node available for split chunks; file-level chunks are fallbacks).

### Step 3: Store calls_out in Qdrant

**File:** `Libraries\CodeAssist.Core\Services\QdrantService.cs`

**In UpsertChunksAsync (line 86-103):** Add `calls_out` to the payload. Store as a Qdrant keyword list using `ListValue` so filtering works:

```csharp
// After existing payload fields
["calls_out"] = chunk.CallsOut is { Count: > 0 }
    ? new Value { ListValue = BuildStringList(chunk.CallsOut) }
    : new Value { ListValue = new ListValue() }
```

**In SearchAsync (line 156-177):** Parse `calls_out` back from payload. Add helper `ParseCallsOut` that checks for the key, reads the ListValue, and returns `IReadOnlyList<string>?`.

**Extract `BuildChunkFromPayload` helper** to avoid duplicating the CodeChunk-from-payload construction (currently inline in SearchAsync). Reuse in new methods below.

**Add `CreatePayloadIndexAsync`** -- creates a keyword index on a field for efficient filtering. Non-fatal on failure (just logs warning).

**Add `SearchBySymbolNamesAsync`** -- scrolls for chunks where `symbol_name` matches any given name. Uses Qdrant `Match { Keyword = symbolName }` filter. Returns `List<SearchResult>` with score 0.0 (not vector-scored).

**Add `SearchCallersOfAsync`** -- scrolls for chunks where `calls_out` contains a given symbol name. Same filter approach on the keyword list field.

**In UpsertPointsAsync (line 345 switch):** Add cases for `IReadOnlyList<string>` and `string[]` to convert to `ListValue`. This is needed for L2 promotion payloads.

### Step 4: Create payload indexes during indexing

**File:** `Libraries\CodeAssist.Core\Services\RepositoryIndexer.cs`

After `EnsureCollectionAsync` call (around line 58), add:

```csharp
await qdrantService.CreatePayloadIndexAsync(collectionName, "symbol_name");
await qdrantService.CreatePayloadIndexAsync(collectionName, "calls_out");
```

Idempotent -- no-ops if indexes already exist.

### Step 5: Update L2 promotion payload

**File:** `Libraries\CodeAssist.Core\Caching\L2PromotionService.cs`

In `ProcessBatchAsync` (line 183-196), add `calls_out` to the payload dictionary:

```csharp
["calls_out"] = (object?)chunk.CallsOut ?? Array.Empty<string>()
```

The existing `UpsertPointsAsync` switch (updated in Step 3) handles converting this to a ListValue.

### Step 6: Dependency expansion in UnifiedSearchService

**File:** `Libraries\CodeAssist.Core\Caching\UnifiedSearchService.cs`

Add `bool includeDependencies = false` parameter to `SearchAsync`.

Add `DependencyResults` property (nullable list) to `UnifiedSearchResult`.
Add `DependencyType` property (string?, "caller" or "callee") to `UnifiedSearchHit`.
Add `DependencyGraph` value to `SearchSource` enum.

Add private `ExpandDependenciesAsync` method:

1. Collect all `CallsOut` entries from primary hits into a set
2. Collect all `SymbolName` values from primary hits into a set
3. Call `SearchBySymbolNamesAsync` to find callees (chunks whose symbol_name matches any calls_out entry)
4. Call `SearchCallersOfAsync` for each primary hit's symbol_name to find callers
5. Deduplicate against primary hit IDs
6. Return as separate list tagged with DependencyType

### Step 7: Expose in search tools

**File:** `CodeAssistMcp\McpTools\SearchTools.cs`

Add `bool includeDependencies = false` parameter to `SearchCode`, `FindSimilarCode`, `ExplainCodeArea`, and `SearchBySymbol`. Pass through to `unifiedSearch.SearchAsync`.

In the response JSON, add `dependencyResults` section (null if not requested) containing: filePath, startLine, endLine, chunkType, symbolName, language, content, dependencyType, source.

## Implementation Order

1. CodeChunk model (CallsOut property)
2. TreeSitterChunker (call extraction methods + wiring)
3. QdrantService (storage, retrieval, dependency queries, payload indexes)
4. RepositoryIndexer (payload index creation)
5. L2PromotionService (calls_out in promotion payload)
6. UnifiedSearchService (dependency expansion, updated result types)
7. SearchTools (includeDependencies parameter)
8. CodeAssistOptions (VoiceProfilePath)
9. PersonalContextTools (new file)
10. Program.cs (register PersonalContextTools)
11. appsettings.json (VoiceProfilePath value)

Steps 1-7 are Feature 2. Steps 8-11 are Feature 1 (can be done at any point since it's independent).

## Verification

**Voice profile:** Build, invoke `get_voice_profile` -- should return markdown content. Test with missing/blank path for error handling.

**Dependency graph:**

1. Build succeeds
2. Re-index a repository (e.g., CodeAssist itself)
3. Check Qdrant payload via REST (`GET http://192.168.0.170:6333/collections/{name}/points/scroll`) to confirm `calls_out` is populated on method/function chunks
4. `search_code` with `includeDependencies=false` -- identical to current behavior, but chunks now include callsOut data
5. `search_code` with `includeDependencies=true` -- dependencyResults section appears with callers/callees
6. Old indexes without `calls_out` still work (ParseCallsOut returns null gracefully)

## Key Files

- `C:\Users\jorda\RiderProjects\McpServers\Libraries\CodeAssist.Core\Models\CodeChunk.cs`
- `C:\Users\jorda\RiderProjects\McpServers\Libraries\CodeAssist.Core\Chunking\TreeSitterChunker.cs`
- `C:\Users\jorda\RiderProjects\McpServers\Libraries\CodeAssist.Core\Services\QdrantService.cs`
- `C:\Users\jorda\RiderProjects\McpServers\Libraries\CodeAssist.Core\Services\RepositoryIndexer.cs`
- `C:\Users\jorda\RiderProjects\McpServers\Libraries\CodeAssist.Core\Caching\L2PromotionService.cs`
- `C:\Users\jorda\RiderProjects\McpServers\Libraries\CodeAssist.Core\Caching\UnifiedSearchService.cs`
- `C:\Users\jorda\RiderProjects\McpServers\CodeAssistMcp\McpTools\SearchTools.cs`
- `C:\Users\jorda\RiderProjects\McpServers\CodeAssistMcp\McpTools\PersonalContextTools.cs` (new)
- `C:\Users\jorda\RiderProjects\McpServers\CodeAssistMcp\Program.cs`
- `C:\Users\jorda\RiderProjects\McpServers\Libraries\CodeAssist.Core\Configuration\CodeAssistOptions.cs`
- `C:\Users\jorda\RiderProjects\McpServers\CodeAssistMcp\appsettings.json`
