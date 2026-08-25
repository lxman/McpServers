# CodeAssist Development Handoff

Updated 2026-08-25.

## Operational constraint

- Work only with Debug builds. The Release build is currently in use.
- Do not rebuild, replace, or restart `CodeAssistMcp/bin/Release/net10.0/CodeAssistMcp.dll`
  without explicit approval.

## Current working-tree state

The uncommitted changes improve CodeAssist graph correctness, search quality, MCP response size,
and index freshness reporting. Inspect them with `git diff` and `git status --short` before doing
more work. No Release artifacts were changed.

Implemented behavior:

- Split `class_partN` and `method_partN` chunks are canonicalized into logical graph symbols.
- Graph edges use qualified, receiver-type, same-type, or DI-aware resolution. Unsafe global bare
  method and field matching is intentionally omitted.
- Overlapping class/method call edges and identical graph edges are deduplicated.
- Ambiguous type lookups return candidates instead of silently choosing one.
- Graph and reporting tools have bounded defaults, filters, and `returned`/`truncated` metadata.
  Trace and impact limits are applied during traversal as well as serialization.
- Bounded bidirectional traces retain nearest nodes first and return only edges whose endpoints are
  present in the bounded node set. Their node totals include the root consistently.
- Symbol search runs exact Qdrant and semantic lookup concurrently, ranks exact matches first, and
  honors `symbolType`.
- Exact symbol hits from files present in the L1 hot cache are re-resolved against the fresh chunks;
  renamed or removed stale Qdrant symbols are not returned.
- Semantic L2 hits are checked directly against the hot cache even when the file produced no L1
  candidate. Stale chunks with no fresh counterpart are dropped.
- Newly indexed or promoted chunks store indexed `canonical_symbol_name` values, so split
  methods/classes resolve without treating every member of a requested class as an exact match. A
  filtered `parent_symbol` fallback remains for legacy chunks until they are re-upserted by a full
  reindex, explicit backfill, or later file changes.
- Exact symbol lookup merges current and legacy matches instead of letting one current match suppress
  legacy split definitions. Canonical qualified names support dependency lookup for split callees.
- Semantic results are diversified to reduce overlapping chunks and repeated files.
- Search tools expose path, language, test, and documentation filters. Filtering occurs before final
  diversification against an expanded candidate pool.
- Test filtering recognizes root-level `test`/`tests` directories and `*.Tests` project directories.
- Dependency expansion accepts explicit qualified targets, receiver types, and conservative
  unqualified same-type calls from callable chunks. Receiver-bearing unresolved calls remain omitted.
- Index state distinguishes `lastFullIndexAt` from `lastPromotionAt`.
- Partial refreshes preserve the prior complete-refresh timestamp and commit SHA, and persist their
  failed-file list for `list_indexes` and `get_index_status`.
- `parent_symbol` is included in payload indexes; the invalid `calls_out` payload index was removed
  from future schema setup.
- Trace and impact tools reject ambiguous bare symbols, report missing symbols explicitly, and keep
  returned/truncated node counts consistent with the included root node.
- Ambiguous receiver-type, same-type, DI, and containing-type graph targets remain unresolved rather
  than creating multiple plausible edges.
- Search, graph, `index_repository`, and `refresh_index` propagate request cancellation through
  asynchronous index, embedding, Qdrant, and graph-build operations. Cancellation is no longer
  recorded as a failed source file or converted into a normal JSON failure response.
- The most recently active repository is persisted outside the `*.json` index manifests and its
  watcher/promotion mapping is restored by a hosted startup service. `stop_watching_all` clears the
  restart state.
- Required Qdrant payload indexes are read back from collection metadata. Index setup now creates
  only missing indexes, verifies the final schema, and fails indexing if a required field is still
  absent. `get_index_status` reports schema readiness, missing fields, or a verification error.

## Verification completed

Debug build:

```powershell
dotnet build CodeAssistMcp\CodeAssistMcp.csproj -c Debug --no-restore -m:1 -v minimal
```

Result: 0 warnings, 0 errors.

Tests:

```powershell
dotnet build Libraries\CodeAssist.Core.Tests\CodeAssist.Core.Tests.csproj -c Debug --no-restore -m:1 -v quiet
dotnet Libraries\CodeAssist.Core.Tests\bin\Debug\net10.0\CodeAssist.Core.Tests.dll -noColor
```

Result: 125 total, 121 passed, 4 skipped. The skipped tests are the existing opt-in live-service
integration tests requiring `CODEASSIST_TEST_QDRANT_URL` and `CODEASSIST_TEST_OLLAMA_URL`.

The configured Qdrant service was queried directly after the schema-verification change. The
`mcpservers`, `pdflibrary`, and `pellucid` collections are green and contain every currently required
payload index. This validates the metadata shape used by `GetMissingPayloadIndexesAsync` against the
deployed Qdrant version.

The Debug MCP binary was also exercised directly over stdio against the live services. Observed
improvements on the `McpServers` index:

- `ISemanticSearchBackend` implementations: 36 to 1.
- Graph nodes: 12,646 to 8,664.
- Graph edges: 59,683 to 2,745.
- Cross-component edges: 52,160 to 427.
- Qualified `L2PromotionService.ProcessBatchAsync` tracing resolves correctly.
- Exact `BuildRelativePathFilter` symbol search returns the definition first.
- Bare `Dispose` tracing returns an ambiguity response with qualified candidates rather than picking
  an arbitrary method.
- Legacy-index lookup for `QdrantService` returns its split definition and same-named constructor as
  exact hits, not unrelated methods whose containing type is `QdrantService`.

## Deployment completed

The configured CodeAssist MCP server was restarted and verified on 2026-08-25. Its tool schemas
include the new limits and filters, and Ollama/Qdrant both reported healthy.

`refresh_index` completed successfully for all existing repositories:

- `McpServers`: 26 files processed, 981 skipped, 13,632 total chunks.
- `PdfLibrary`: 3 files processed, 1,648 skipped, 23,321 total chunks.
- `Pellucid`: 44 files processed, 1,153 skipped, 19,415 total chunks.

All three states now have `lastFullIndexAt`, `lastIndexComplete: true`, no failed files, and
`manifestIsLegacy: false`.

Post-restart smoke tests passed:

- `ISemanticSearchBackend` resolves to one implementation.
- `BuildRelativePathFilter` exact search returns its definition first.
- `QdrantService` exact search returns both its split definition and constructor.
- Qualified bidirectional tracing of `L2PromotionService.ProcessBatchAsync` returns 9 of 9 nodes and
  8 of 8 edges with `truncated: false`.
- Bare `Dispose` tracing returns qualified ambiguity candidates.
- Filtered C# search under `Libraries/CodeAssist.Core` returns a full five-result page with no tests
  or documentation.
- Current `McpServers` graph: 8,706 nodes, 2,789 edges, 428 cross-component edges.

The refresh was incremental. Unchanged legacy chunks do not receive canonical-name payload values,
but exact and qualified lookup support them through filtered fallbacks. A full reindex or explicit
payload backfill is optional and only needed if complete payload population is required.

## Post-deployment follow-up verified

Live verification exposed three `PdfLibrary` files that were successfully processed with zero chunks
but were repeatedly reported as newly added because zero-chunk files were omitted from the manifest.
The follow-up records those files with `ChunkCount: 0`, and `refresh_index` returns `complete` plus
`failedFiles` explicitly.

After the follow-up Release restart, the first `PdfLibrary` refresh recorded the three files and the
second reported 0 processed, 0 added, 1,651 skipped, and no failures. `get_index_status` now reports
1,651 files, 23,321 chunks, `lastIndexComplete: true`, and `manifestIsLegacy: false`.

The high-confidence graph intentionally favors precision over recall. Calls that lack qualified,
receiver-type, same-type, or DI resolution remain unresolved instead of producing plausible but
false cross-component edges.

## Follow-up deployment completed

The restart-safe watcher, same-type dependency expansion, indexing cancellation, and payload-schema
verification changes described above were deployed to
`CodeAssistMcp/bin/Release/net10.0/CodeAssistMcp.dll` after explicit approval on 2026-08-25. The old
MCP process was stopped because it held three dependency DLLs open; the subsequent Release build
completed with 0 warnings and 0 errors. The deployed `CodeAssist.Core.dll`, `Mcp.Common.Core.dll`,
and `SerilogFileWriter.dll` byte-match their freshly built Release outputs, and launching the Release
entry point with closed stdin completed successfully as a startup/DI smoke check.

Restart the MCP connection so Codex launches the deployed process, then verify:

1. Search or set an active repository, restart the MCP process, and confirm
   `get_watched_repositories` immediately reports it without a preceding search.
2. Search for `ProcessPromotionQueueAsync` with dependencies and confirm the unqualified
   `ProcessBatchAsync` same-type callee is returned.
3. Confirm `get_index_status` reports `payloadIndexesReady: true` and an empty
   `missingPayloadIndexes` list for all three repositories.

The first post-deployment restart loaded the new Release successfully. `check_health` reports both
services healthy; all three repository statuses report `payloadIndexesReady: true`, no missing
payload indexes, and no schema-check error. A live dependency-expanded search for
`ProcessPromotionQueueAsync` returns the unqualified same-type `ProcessBatchAsync` callee (including
its split chunks).

The pre-upgrade process had no persistence feature, so the first restart correctly began with no
restart repository. `set_active_repository("McpServers")` then started its watcher and returned
`restartStateSaved: true`. After a second MCP connection restart, and before any search or repository
selection call, `get_watched_repositories` reported the `McpServers` path already watched,
`restartRepository: "McpServers"`, zero pending promotions, and zero dropped promotions. Startup
restoration is therefore verified end to end. The accompanying health check remained fully green.
