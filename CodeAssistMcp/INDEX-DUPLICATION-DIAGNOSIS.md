# Index duplication — diagnosis, 2026-08-24

Written after a day of heavy editing in two watched repos (`PdfLibrary`, `Pellucid`) turned the
search index into something that returns **five copies of the same method**, each a different version
of the file. This is what I observed, what I traced it to, and what I could not confirm.

---

## TL;DR

Two independent defects in the update path, either of which alone would cause the duplication:

1. **The full indexer and the file watcher write `relative_path` with different directory
   separators** — `/` from the indexer, `\` from the watcher. Deleting by one form cannot match rows
   stored under the other.
2. **`DeleteByFilePathAsync` filters with Qdrant's full-text `Match { Text = ... }` on
   `relative_path`, and `relative_path` has no payload index** — it is absent from
   `EnsurePayloadIndexesAsync`'s field list.

Net effect: an updated file's old chunks are never removed, so every edit leaves another complete
copy behind. `refresh_index` makes it **worse**, not better — it adds one more copy per changed file
and reports `filesRemoved: 0`.

---

## What is NOT wrong — please don't spend time here

**The watchers are alive and working.** I initially misdiagnosed this as dead watchers and want to
save the next person the same detour. Evidence they are running:

- `get_watched_repositories` reports both repos, and `hotCacheFileCount` moved `0 → 11` over the
  course of an editing session.
- A symbol I introduced (`MinExtent`) returned **0 results** when first searched and **2 results**
  about an hour later. That is promotion latency, not failure.
- Searches return `l1HitCount > 0`, and results carry `source: L2WithL1Content, isFresh: true` — the
  hot cache is actively serving current content.

**Embedding/vector infrastructure is healthy.** `check_health` reports `bge-base-en-v1.5` available,
Qdrant up, 4 collections, dimension 768. Nothing here is a model or connectivity problem.

---

## Observed symptoms

Searching `symbolName: "ClassifyAnnotationTypes"` in `PdfLibrary` returned **5 results, all the same
method**, at line ranges `105-177`, `108-180`, `122-194`, `123-195`, `123-195`. Those are successive
versions of one file as it grew across two commits — every one retained.

Two details in that result set point straight at the causes:

**Mixed separators for the same file:**

```
PdfLibrary/Editing/PdfDocumentEditor.AnnotationTypes.cs    ← forward slashes
PdfLibrary\Editing\PdfDocumentEditor.AnnotationTypes.cs    ← backslashes
```

**The same chunk stored twice differing only in line endings** (`\r\n` vs `\n`) — I had written the
file with LF and git later normalised it to CRLF; both versions were indexed and both kept.

`refresh_index` on `PdfLibrary`: `filesProcessed: 16, filesAdded: 10, filesUpdated: 6,
filesRemoved: 0`, and `totalChunks` rose **22,955 → 23,158**.

---

## Root cause 1 — the two writers disagree about separators

`Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs:381-401`, `DiscoverFiles`, returns
`result.Files.Select(f => f.Path)` from `Microsoft.Extensions.FileSystemGlobbing.Matcher`. That
library yields **forward-slash** relative paths on every platform.

`Libraries/CodeAssist.Core/Caching/HotCache.cs:108`:

```csharp
string relativePath = Path.GetRelativePath(repositoryRoot, filePath);
```

On Windows that returns **backslashes**.

Both values flow unmodified into `CodeChunk.RelativePath` (`RepositoryIndexer.cs:146`,
`HotCache.cs:137`) and are persisted as the `relative_path` payload field
(`Services/QdrantService.cs:147`). **There is no separator normalisation anywhere in the codebase** —
I grepped for it; the only `Replace('\\','/')` is in `DataFlowGraphService.cs:117` for an unrelated
project-directory computation.

So the same file is two distinct keys, and `RepositoryIndexer`'s delete (which passes the
forward-slash form) can never match rows the watcher wrote.

## Root cause 2 — deleting by an unindexed full-text field

`Services/QdrantService.cs:254-278`:

```csharp
var filter = new Filter {
    Must = { new Condition { Field = new FieldCondition {
        Key = "relative_path",
        Match = new Match { Text = relativePath }
    } } }
};
await GetClient().DeleteAsync(collectionName, filter, ...);
```

`Match { Text = ... }` is Qdrant's **full-text** match operator, which requires a text payload index
on the field. `EnsurePayloadIndexesAsync` (`QdrantService.cs:772-790`) indexes nine fields —
`qualified_name`, `base_type`, `implemented_interfaces`, `namespace`, `return_type`,
`access_modifier`, `calls_out_names`, `symbol_name`, `chunk_type` — and **`relative_path` is not
among them.**

This is independent of root cause 1: even with separators unified, a `Text` match on an unindexed
field is not going to reliably delete the right rows.

There is a second hazard here worth fixing at the same time. `Text` match is token/substring-based,
not exact. Even working correctly it could match *more* than intended — deleting `Foo.cs` could plausibly
take `SubDir/Foo.cs` with it. An exact keyword match is what this call actually wants.

### Call sites — there are two, and the second is worse

`RepositoryIndexer.cs:125-127`, the update path:

```csharp
if (updateHashSet.Contains(relativePath))
    await qdrantService.DeleteByFilePathAsync(collectionName, relativePath, ct);
```

The guard is right — new files skip the delete, updates delete first. The delete itself is what
no-ops.

`RepositoryIndexer.cs:~87`, the **removal** path:

```csharp
foreach (string file in filesToRemove)
    await qdrantService.DeleteByFilePathAsync(collectionName, file, cancellationToken);
```

Same broken call. This means **a file deleted from the repository is never removed from the index** —
its chunks persist indefinitely and keep being returned by searches. That is arguably worse than the
duplication, because there is no newer copy to outrank the ghost. It also explains why every
`refresh_index` and `index_repository` run in this session reported `filesRemoved: 0`: not because
nothing needed removing, but because the removal cannot work.

### A second payload-index list, also missing `relative_path`

`RepositoryIndexer.cs:63-64` creates payload indexes inline at index time:

```csharp
await qdrantService.CreatePayloadIndexAsync(collectionName, "symbol_name", cancellationToken);
await qdrantService.CreatePayloadIndexAsync(collectionName, "calls_out", cancellationToken);
```

So payload indexes are established in **two** places with **different** field lists — this pair, and
`EnsurePayloadIndexesAsync`'s nine (which includes `symbol_name` but not `calls_out`). Neither
includes `relative_path`. Worth consolidating to one list while fixing root cause 2, or the next
field to need an index will get the same split treatment.

---

## Root cause 3 (minor) — watcher promotions don't update index metadata

`list_indexes` / `get_index_status` report `lastUpdated` and `lastCommitSha`. `refresh_index` updates
them; watcher promotions do not. Before I ran a refresh, `PdfLibrary` reported
`lastUpdated == createdAt` (identical to the tick) and a `lastCommitSha` two days and many commits
stale, while the index in fact contained files created that morning.

This is what sent me down the dead-watcher path. Those fields currently mean "last manual refresh",
not "last update", and anything reading them as a freshness signal will be wrong.

---

## What I did *not* verify

Stated plainly so it gets checked rather than assumed:

- **I did not instrument `DeleteByFilePathAsync` to prove it deletes zero points.** The conclusion is
  inferred from `filesRemoved: 0`, the rising chunk count, and the observed duplicates. Confirm it
  directly — log the Qdrant delete response, or query the collection for a known `relative_path`
  before and after.
- **I did not confirm Qdrant's exact behaviour for `Match { Text }` on an unindexed field** in the
  version in use. It may error, match nothing, or silently degrade. Worth establishing before
  choosing the fix.
- **I did not check whether other call sites share these bugs.**
  `DataFlowGraphService.cs:169-173` also removes nodes by relative path and may have the same
  separator exposure.
- **I did not determine which writer produced which of the five duplicate copies**, only that both
  separator forms are present.

---

## Suggested fix

1. **Normalise `relative_path` at the point of construction**, not at the call sites — one helper,
   used by both `RepositoryIndexer.DiscoverFiles` and `HotCache`, forcing `/`. Forward slashes are
   the better target: the globbing library already produces them, they are stable across platforms,
   and existing forward-slash rows stay valid.
2. **Switch `DeleteByFilePathAsync` to an exact keyword match** (`Match { Keyword = ... }` or the
   equivalent for the client version), and **add `relative_path` to `EnsurePayloadIndexesAsync`** with
   the matching index type.
3. **Have `refresh_index` and watcher promotion both update `lastUpdated` / `lastCommitSha`**, so the
   metadata means what its name says.
4. **One-off cleanup**: existing collections carry accumulated duplicates. `refresh_index` cannot
   clear them (it only adds). `delete_index` + `index_repository` does — **confirmed on `PdfLibrary`
   2026-08-24**: the `ClassifyAnnotationTypes` search went from 5 results to 1, at the correct line
   range with current content and a forward-slash path. The rebuild took 15m41s for 1,647 files.

   A note on reading chunk counts, because they misled me and will mislead the next person.
   `PdfLibrary` went 22,955 (stale, duplicated) → 23,158 (after refresh) → 23,158 (after clean
   rebuild), and the clean rebuild matching the duplicated count looks alarming. It isn't: the old
   index covered 1,637 files and the clean one covers 1,647, so the counts are not comparable. A
   duplicated index can hold *fewer* chunks than a clean one if it is also missing files.
   **`totalChunks` is not a duplication signal — search for a known symbol and count the results.**

### Regression test worth having

The bug is invisible to any test that indexes a file once. It needs: index a file, **modify it**,
re-index, then assert the chunk count for that path did not grow and no stale content is returned.
Run it once through the full indexer path and once through the watcher/hot-cache path, since the two
disagree — a test that exercises only one would have passed throughout.

---

## Reproduction

1. Index a repo; note `totalChunks`.
2. Edit a source file (change a method body so a chunk's content and line range both move).
3. Wait for watcher promotion, or run `refresh_index`.
4. Search for a symbol in that file — you get the old and new versions both.
5. `list_indexes` — `totalChunks` has grown; `filesRemoved` was `0`.

Observed at scale in `PdfLibrary` (1,644 files / 23,158 chunks) and `Pellucid` (1,164 / 18,738) on
2026-08-24.
