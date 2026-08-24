# CodeAssist Index Duplication Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the CodeAssist search index from accumulating stale duplicate copies of every edited file, and make index freshness metadata mean what its name says.

**Architecture:** Three writers touch `relative_path` in Qdrant — the full indexer, the L1→L2 promotion path, and the graph rebuilder — and they currently disagree about path format, delete semantics, and whether a delete happens at all. This plan gives `relative_path` a single normalized form (`/`), makes every lookup on it an exact keyword match backed by a payload index, adds the missing delete-before-upsert on the promotion path, and extracts index state into a store both writers can update. A narrow `IQdrantWriter` interface is introduced so the promotion path's delete-then-upsert ordering is unit-testable without a live Qdrant.

**Tech Stack:** C# / .NET 10, Qdrant (via `Microsoft.SemanticKernel.Connectors.Qdrant`, `Qdrant.Client.Grpc` types), xunit.v3, central package management.

**Spec:** `CodeAssistMcp/INDEX-DUPLICATION-DIAGNOSIS.md`

## Global Constraints

- Target framework is `net10.0`; `<Nullable>enable</Nullable>`. Match the existing style of the file you are editing.
- Central package management is on (`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`). **Never put a `Version=` attribute on a `PackageReference`** — add a `PackageVersion` entry to `Directory.Packages.props` instead.
- The solution file is `McpServers.slnx` (XML solution format), not a `.sln`.
- Normalized `relative_path` form is **forward slashes**. Existing rows already use forward slashes, so this keeps them valid.
- **Never lowercase a path.** Casing is significant on Linux and Qdrant keyword matching is case-sensitive.
- Qdrant keyword match idiom in this codebase is `Match { Keyword = value }`. Full-text is `Match { Text = value }`. See `ScrollWithKeywordFilterAsync` for the established pattern.
- Do not run `delete_index` / `index_repository` against any real collection as part of this work. The one-off reindex is a separate follow-up, after these fixes ship.
- **Every test file needs an explicit `using Xunit;`.** Verified during Task 1: xunit.v3 3.2.2 does not contribute a global using for `Xunit`, and the project's generated `GlobalUsings.g.cs` carries only the standard SDK set. Without it, `Fact`, `Theory`, `InlineData`, `Assert`, and `IAsyncLifetime` do not resolve.

## Verified findings this plan is built on

These were confirmed against the code and a live Qdrant at `http://192.168.0.170:6333` on 2026-08-24. Two of them correct the spec:

1. **Separator mismatch is real.** `DiscoverFiles` returns forward slashes from `Microsoft.Extensions.FileSystemGlobbing`; `HotCache.cs:108` uses `Path.GetRelativePath` which returns backslashes on Windows. Chunkers pass `relativePath` through verbatim. Measured: a text match on the backslash form of a real file returns **0** points; the forward-slash form returns **32**.
2. **Spec correction — `Match { Text }` on an unindexed field does NOT fail.** Qdrant full-scans and matches. Measured: full path via `Text` → 32 points (0.34s over 23,158); same path via `Keyword` → 32 points. The real defect is that `Text` is **tokenized and over-broad**: the bare token `Editing` matched **1,963** points. So the fix is for over-deletion and speed, not to make deletion work at all.
3. **Spec correction — `filesRemoved: 0` is a red herring.** `RepositoryIndexer.cs:258` sets `FilesRemoved = filesToRemove.Count`, computed in `CategorizeFiles` from state-file keys not present on disk. Both sides are forward-slash, so the count is correct; `0` means nothing was detected as deleted, not that removal failed.
4. **The dominant cause is not in the spec: `L2PromotionService.ProcessBatchAsync` never deletes prior chunks before upserting.** `DeleteByFilePathAsync` has exactly two callers, both in `RepositoryIndexer`; nothing under `Caching/` ever deletes from Qdrant. Every chunk gets a fresh `Guid.NewGuid()` at chunk time, so upsert can never overwrite by ID. Every watcher promotion of an edited file therefore appends a complete additional copy, unconditionally.
5. **`OnFileDeleted` / `OnFileRenamed` only evict from L1.** A deleted or renamed file's chunks persist in Qdrant until a full refresh.
6. **`SearchByFilePathAsync` silently caps at 100 chunks** (`ScrollWithKeywordFilterAsync` default `limit: 100`). Files exceeding that exist today — `NetworkingDtos.cs` holds 368 chunks — so graph rebuild sees a truncated file.
7. **Payload indexes are created in two places with different lists.** `RepositoryIndexer.cs:62-64` creates `symbol_name` and `calls_out`; `EnsurePayloadIndexesAsync` creates nine others. Neither includes `relative_path`.
8. **The graph-rebuild exposure the spec suspected is real but latent.** `DataFlowGraphService.RebuildFileAsync` does pass an un-normalized `relativePath` to both `RemoveNodesByFile` and `SearchByFilePathAsync` — but it is `public` with **no callers anywhere in the solution**, so nothing exercises it today. Task 3 normalizes at its entry rather than leaving a trap for the first caller.

---

### Task 1: Test project and the path normalization helper

Nothing in this solution has tests. This task stands up the first test project and delivers the helper every later task depends on.

**Files:**
- Create: `Libraries/CodeAssist.Core/Services/IndexPath.cs`
- Create: `Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
- Create: `Libraries/CodeAssist.Core.Tests/Services/IndexPathTests.cs`
- Modify: `Directory.Packages.props`
- Modify: `McpServers.slnx`
- Modify: `Libraries/CodeAssist.Core/CodeAssist.Core.csproj` (add `InternalsVisibleTo`)

**Interfaces:**
- Consumes: nothing.
- Produces: `public static class CodeAssist.Core.Services.IndexPath` with `public static string Normalize(string relativePath)`. Every later task calls this.

- [ ] **Step 1: Add test package versions**

In `Directory.Packages.props`, add inside the existing `<ItemGroup>` of `PackageVersion` entries (versions copied from the Pellucid solution so the two stay aligned):

```xml
<PackageVersion Include="xunit.v3" Version="3.2.2" />
<PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
<PackageVersion Include="coverlet.collector" Version="10.0.1" />
```

- [ ] **Step 2: Create the test project**

Create `Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" />
        <PackageReference Include="xunit.runner.visualstudio" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="coverlet.collector" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\CodeAssist.Core\CodeAssist.Core.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Register the project in the solution**

In `McpServers.slnx`, inside the `<Folder Name="/Libraries/">` element, add:

```xml
<Project Path="Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj" />
```

- [ ] **Step 4: Expose internals to the test project**

In `Libraries/CodeAssist.Core/CodeAssist.Core.csproj`, add a new `ItemGroup`:

```xml
<ItemGroup>
    <InternalsVisibleTo Include="CodeAssist.Core.Tests" />
</ItemGroup>
```

- [ ] **Step 5: Write the failing tests**

Create `Libraries/CodeAssist.Core.Tests/Services/IndexPathTests.cs`:

```csharp
using CodeAssist.Core.Services;

namespace CodeAssist.Core.Tests.Services;

public class IndexPathTests
{
    [Fact]
    public void Normalize_ConvertsBackslashesToForwardSlashes()
    {
        Assert.Equal(
            "PdfLibrary/Editing/PdfDocumentEditor.AnnotationTypes.cs",
            IndexPath.Normalize(@"PdfLibrary\Editing\PdfDocumentEditor.AnnotationTypes.cs"));
    }

    [Fact]
    public void Normalize_LeavesForwardSlashPathsUnchanged()
    {
        const string path = "PdfLibrary/Editing/PdfDocumentEditor.AnnotationTypes.cs";
        Assert.Equal(path, IndexPath.Normalize(path));
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        string once = IndexPath.Normalize(@"a\b\c.cs");
        Assert.Equal(once, IndexPath.Normalize(once));
    }

    [Fact]
    public void Normalize_HandlesMixedSeparators()
    {
        Assert.Equal("a/b/c.cs", IndexPath.Normalize(@"a/b\c.cs"));
    }

    [Fact]
    public void Normalize_StripsLeadingSeparators()
    {
        Assert.Equal("a/b.cs", IndexPath.Normalize(@"\a\b.cs"));
        Assert.Equal("a/b.cs", IndexPath.Normalize("/a/b.cs"));
    }

    [Fact]
    public void Normalize_StripsLeadingCurrentDirectoryPrefix()
    {
        Assert.Equal("a/b.cs", IndexPath.Normalize(@".\a\b.cs"));
        Assert.Equal("a/b.cs", IndexPath.Normalize("./a/b.cs"));
    }

    [Fact]
    public void Normalize_PreservesCasing()
    {
        Assert.Equal("PdfLibrary/Editing/Foo.cs", IndexPath.Normalize(@"PdfLibrary\Editing\Foo.cs"));
    }

    [Fact]
    public void Normalize_PassesThroughEmptyInput()
    {
        Assert.Equal("", IndexPath.Normalize(""));
    }

    [Fact]
    public void Normalize_ThrowsOnNullInput()
    {
        // The signature promises non-nullable in and out. Returning null for null input would hand a
        // nullable-enabled caller an unwarned NRE somewhere downstream instead of failing here.
        Assert.Throws<ArgumentNullException>(() => IndexPath.Normalize(null!));
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: FAIL to compile — `IndexPath` does not exist.

- [ ] **Step 7: Write the implementation**

Create `Libraries/CodeAssist.Core/Services/IndexPath.cs`:

```csharp
namespace CodeAssist.Core.Services;

/// <summary>
/// The single source of truth for the form of a <c>relative_path</c> payload value.
/// </summary>
/// <remarks>
/// This exists because there were two forms. <see cref="RepositoryIndexer"/> discovers files through
/// <c>Microsoft.Extensions.FileSystemGlobbing</c>, which yields forward slashes on every platform, while
/// <c>HotCache</c> computes its path with <see cref="Path.GetRelativePath"/>, which yields backslashes on
/// Windows. The same file was therefore stored under two distinct keys, and a delete issued in one form
/// could not match rows written in the other — measured against a live collection, the backslash form of a
/// real file matched zero points while the forward-slash form matched thirty-two.
///
/// <para>Forward slashes are the target because existing rows already use them, the globbing library
/// produces them unprompted, and they are stable across platforms. Casing is deliberately preserved:
/// it is significant on Linux and Qdrant keyword matching is case-sensitive.</para>
///
/// <para>Normalize at the point a relative path is constructed, not at the call sites that consume it.
/// Two functions that must agree, applied separately, will eventually disagree.</para>
/// </remarks>
public static class IndexPath
{
    /// <summary>
    /// Convert a relative path to its canonical index form: forward slashes, no leading separator,
    /// no leading "./" segment.
    /// </summary>
    public static string Normalize(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        if (relativePath.Length == 0) return relativePath;

        string normalized = relativePath.Replace('\\', '/');

        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/');
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: PASS, 9 tests.

- [ ] **Step 9: Commit**

```bash
git add Directory.Packages.props McpServers.slnx Libraries/CodeAssist.Core/CodeAssist.Core.csproj Libraries/CodeAssist.Core/Services/IndexPath.cs Libraries/CodeAssist.Core.Tests
git commit -m "Give relative_path one canonical form, and the solution its first test project"
```

---

### Task 2: Normalize at both writers

Both places that construct a relative path now produce the same form. Each gets an extracted, testable seam.

**Files:**
- Modify: `Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs` (`DiscoverFiles`, ~line 381)
- Modify: `Libraries/CodeAssist.Core/Caching/HotCache.cs` (~line 108)
- Create: `Libraries/CodeAssist.Core.Tests/Services/DiscoverFilesTests.cs`
- Create: `Libraries/CodeAssist.Core.Tests/Caching/HotCacheRelativePathTests.cs`

**Interfaces:**
- Consumes: `IndexPath.Normalize` from Task 1.
- Produces: `internal static List<string> RepositoryIndexer.DiscoverFiles(string repositoryPath, IReadOnlyList<string> includePatterns, IReadOnlyList<string> excludePatterns)` — visibility widened from `private` to `internal` for testing. `internal static string HotCache.ComputeRelativePath(string repositoryRoot, string filePath)` — new extracted method.

- [ ] **Step 1: Write the failing test for the indexer path**

Create `Libraries/CodeAssist.Core.Tests/Services/DiscoverFilesTests.cs`:

```csharp
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class DiscoverFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "codeassist-discover-" + Guid.NewGuid().ToString("N"));

    public DiscoverFilesTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Editing", "Nested"));
        File.WriteAllText(Path.Combine(_root, "Top.cs"), "class Top {}");
        File.WriteAllText(Path.Combine(_root, "Editing", "Mid.cs"), "class Mid {}");
        File.WriteAllText(Path.Combine(_root, "Editing", "Nested", "Deep.cs"), "class Deep {}");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void DiscoverFiles_ReturnsForwardSlashPathsOnly()
    {
        List<string> files = RepositoryIndexer.DiscoverFiles(_root, ["*.cs"], []);

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.DoesNotContain('\\', f));
    }

    [Fact]
    public void DiscoverFiles_FindsNestedFilesWithForwardSlashSeparators()
    {
        List<string> files = RepositoryIndexer.DiscoverFiles(_root, ["*.cs"], []);

        Assert.Contains("Editing/Nested/Deep.cs", files);
        Assert.Contains("Editing/Mid.cs", files);
        Assert.Contains("Top.cs", files);
    }
}
```

- [ ] **Step 2: Write the failing test for the watcher path**

Create `Libraries/CodeAssist.Core.Tests/Caching/HotCacheRelativePathTests.cs`:

```csharp
using CodeAssist.Core.Caching;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class HotCacheRelativePathTests
{
    [Fact]
    public void ComputeRelativePath_ReturnsForwardSlashesRegardlessOfPlatform()
    {
        string root = Path.Combine(Path.GetTempPath(), "repo");
        string file = Path.Combine(root, "Editing", "Nested", "Deep.cs");

        string relative = HotCache.ComputeRelativePath(root, file);

        Assert.Equal("Editing/Nested/Deep.cs", relative);
        Assert.DoesNotContain('\\', relative);
    }

    [Fact]
    public void ComputeRelativePath_AgreesWithTheIndexerForm()
    {
        // The two writers must produce byte-identical keys or a delete issued by one
        // cannot match rows written by the other.
        string root = Path.Combine(Path.GetTempPath(), "repo");
        string file = Path.Combine(root, "Top.cs");

        Assert.Equal("Top.cs", HotCache.ComputeRelativePath(root, file));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: FAIL to compile — `DiscoverFiles` is private, `ComputeRelativePath` does not exist.

- [ ] **Step 4: Normalize in the indexer**

In `Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs`, change the `DiscoverFiles` signature from `private static` to `internal static`. Replace:

```csharp
    private static List<string> DiscoverFiles(
```

with:

```csharp
    internal static List<string> DiscoverFiles(
```

and replace the return statement:

```csharp
        return result.Files.Select(f => f.Path).ToList();
```

with:

```csharp
        // Normalize here rather than at the consumers: this value becomes the relative_path payload
        // key, and it must be byte-identical to what HotCache produces for the same file.
        return result.Files.Select(f => IndexPath.Normalize(f.Path)).ToList();
```

- [ ] **Step 5: Normalize in the hot cache**

In `Libraries/CodeAssist.Core/Caching/HotCache.cs`, replace line ~108:

```csharp
            string relativePath = Path.GetRelativePath(repositoryRoot, filePath);
```

with:

```csharp
            string relativePath = ComputeRelativePath(repositoryRoot, filePath);
```

Then add this method to the class, next to the other private helpers:

```csharp
    /// <summary>
    /// The watcher's relative path, in the same canonical form the full indexer produces.
    /// </summary>
    /// <remarks>
    /// <see cref="Path.GetRelativePath"/> returns backslashes on Windows. Left unnormalized, the watcher
    /// wrote a different <c>relative_path</c> key than the indexer for the very same file, so neither
    /// could delete the other's rows and every edit left a complete stale copy behind.
    /// </remarks>
    internal static string ComputeRelativePath(string repositoryRoot, string filePath) =>
        IndexPath.Normalize(Path.GetRelativePath(repositoryRoot, filePath));
```

Add `using CodeAssist.Core.Services;` to the file's usings if it is not already present.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: PASS, 13 tests.

- [ ] **Step 7: Commit**

```bash
git add Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs Libraries/CodeAssist.Core/Caching/HotCache.cs Libraries/CodeAssist.Core.Tests
git commit -m "Make both index writers agree on the shape of a relative path"
```

---

### Task 3: Exact keyword matching on relative_path, and one payload index list

**Files:**
- Modify: `Libraries/CodeAssist.Core/Services/QdrantService.cs` (`SearchAsync` ~206-228, `DeleteByFilePathAsync` ~254-278, `EnsurePayloadIndexesAsync` ~772-790)
- Modify: `Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs:62-64`
- Modify: `Libraries/CodeAssist.Core/Services/DataFlowGraphService.cs` (`RebuildFileAsync`, ~line 152)
- Create: `Libraries/CodeAssist.Core.Tests/Services/RelativePathFilterTests.cs`

Fixing `DeleteByFilePathAsync` itself covers **both** of its call sites — the update path at `RepositoryIndexer.cs:125-127` and the removal path at `RepositoryIndexer.cs:87` — so no separate change is needed for the removal loop.

**Interfaces:**
- Consumes: `IndexPath.Normalize` from Task 1.
- Produces: `internal static Qdrant.Client.Grpc.Filter QdrantService.BuildRelativePathFilter(string relativePath)`, and `relative_path` present in `EnsurePayloadIndexesAsync`'s field list.

- [ ] **Step 1: Write the failing test**

Create `Libraries/CodeAssist.Core.Tests/Services/RelativePathFilterTests.cs`:

```csharp
using CodeAssist.Core.Services;
using Qdrant.Client.Grpc;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class RelativePathFilterTests
{
    [Fact]
    public void BuildRelativePathFilter_UsesExactKeywordNotFullText()
    {
        Filter filter = QdrantService.BuildRelativePathFilter("PdfLibrary/Editing/Foo.cs");

        FieldCondition field = Assert.Single(filter.Must).Field;
        Assert.Equal("relative_path", field.Key);
        Assert.Equal("PdfLibrary/Editing/Foo.cs", field.Match.Keyword);
        // Text match is tokenized: the bare token "Editing" matched 1,963 points in a live
        // collection, so a delete built on it can take unrelated files with it.
        Assert.Equal(Match.MatchValueOneofCase.Keyword, field.Match.MatchValueCase);
    }

    [Fact]
    public void BuildRelativePathFilter_NormalizesBackslashInput()
    {
        Filter filter = QdrantService.BuildRelativePathFilter(@"PdfLibrary\Editing\Foo.cs");

        Assert.Equal("PdfLibrary/Editing/Foo.cs", Assert.Single(filter.Must).Field.Match.Keyword);
    }
}
```

If `Match.MatchValueOneofCase` is named differently on this client version, the compiler will say so — use the generated enum name and keep the assertion.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter RelativePathFilterTests`
Expected: FAIL to compile — `BuildRelativePathFilter` does not exist.

- [ ] **Step 3: Add the shared filter builder**

In `Libraries/CodeAssist.Core/Services/QdrantService.cs`, add near `DeleteByFilePathAsync`:

```csharp
    /// <summary>
    /// The one filter used for every <c>relative_path</c> lookup.
    /// </summary>
    /// <remarks>
    /// Deliberately a keyword (exact) match. The previous full-text <c>Match { Text }</c> did not fail on
    /// the unindexed field as was first assumed — Qdrant full-scanned and matched — but it is tokenized,
    /// so it matches far more than intended: the bare token "Editing" matched 1,963 points in a live
    /// collection. A delete built on that can take unrelated files with it.
    /// </remarks>
    internal static Filter BuildRelativePathFilter(string relativePath) => new()
    {
        Must =
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "relative_path",
                    Match = new Match { Keyword = IndexPath.Normalize(relativePath) }
                }
            }
        }
    };
```

- [ ] **Step 4: Use it in `DeleteByFilePathAsync`**

Replace the filter construction in `DeleteByFilePathAsync` so the `try` block reads:

```csharp
        try
        {
            await GetClient().DeleteAsync(
                collectionName,
                BuildRelativePathFilter(relativePath),
                cancellationToken: cancellationToken);

            _logger.LogDebug("Deleted chunks for file {FilePath} from collection {Collection}", relativePath, collectionName);
        }
```

Leave the existing `catch` block unchanged.

- [ ] **Step 5: Use it in `SearchAsync`**

In `SearchAsync`, replace the `filePathFilter` block:

```csharp
            Filter? filter = null;
            if (!string.IsNullOrEmpty(filePathFilter))
            {
                filter = BuildRelativePathFilter(filePathFilter);
            }
```

This parameter currently has no callers, so the change from substring to exact semantics is unobservable — but it is now consistent with every other `relative_path` lookup.

- [ ] **Step 6: Add `relative_path` to the payload index list and consolidate**

In `EnsurePayloadIndexesAsync`, change the field array to:

```csharp
        string[] indexFields =
        [
            "qualified_name",
            "base_type",
            "implemented_interfaces",
            "namespace",
            "return_type",
            "access_modifier",
            "calls_out_names",
            "calls_out",
            "symbol_name",
            "chunk_type",
            // Without this, every delete and every per-file scroll full-scans the collection.
            "relative_path"
        ];
```

Then in `Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs`, replace lines 62-64:

```csharp
            // Create payload indexes for dependency graph queries
            await qdrantService.CreatePayloadIndexAsync(collectionName, "symbol_name", cancellationToken);
            await qdrantService.CreatePayloadIndexAsync(collectionName, "calls_out", cancellationToken);
```

with:

```csharp
            // One list, one place. Indexes were previously created here AND in EnsurePayloadIndexesAsync
            // with different field sets, so which fields were indexed depended on which path ran.
            await qdrantService.EnsurePayloadIndexesAsync(collectionName, cancellationToken);
```

- [ ] **Step 7: Close the graph-rebuild exposure the spec flagged**

The spec suspected `DataFlowGraphService.cs:169-173` of sharing the separator bug. It does: `RebuildFileAsync` passes its `relativePath` argument straight to `graph.RemoveNodesByFile` and to `SearchByFilePathAsync`, both of which now key on the normalized form. `RebuildFileAsync` currently has **no callers anywhere in the solution** — verify with `grep -rn "RebuildFileAsync" --include=*.cs Libraries/ CodeAssistMcp/ | grep -v /obj/` — so this is latent, not live. Normalize at the entry so a future caller cannot reintroduce the split.

In `Libraries/CodeAssist.Core/Services/DataFlowGraphService.cs`, at the top of `RebuildFileAsync`'s body, before the `_graphs.TryGetValue` guard, add:

```csharp
        // Graph nodes are keyed by the chunk's relative path, which is normalized at construction.
        // A caller handing us a Windows-shaped path would remove nothing and then match nothing.
        relativePath = IndexPath.Normalize(relativePath);
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: PASS, 15 tests.

- [ ] **Step 9: Verify the library still builds**

Run: `dotnet build Libraries/CodeAssist.Core/CodeAssist.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 10: Commit**

```bash
git add Libraries/CodeAssist.Core/Services/QdrantService.cs Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs Libraries/CodeAssist.Core/Services/DataFlowGraphService.cs Libraries/CodeAssist.Core.Tests
git commit -m "Match relative_path exactly, and index it — one payload index list, not two"
```

---

### Task 4: Stop truncating per-file chunk reads at 100

Not in the spec; found while verifying it. `SearchByFilePathAsync` feeds the graph rebuild, and files exceeding 100 chunks exist today (`NetworkingDtos.cs` holds 368), so the graph is being rebuilt from a truncated view of those files. **This task is severable — drop it if you want to keep this branch strictly to the spec.**

**Files:**
- Modify: `Libraries/CodeAssist.Core/Services/QdrantService.cs` (`ScrollWithKeywordFilterAsync`)

**Interfaces:**
- Consumes: nothing new.
- Produces: `ScrollWithKeywordFilterAsync` pages to exhaustion; its public callers' signatures are unchanged.

- [ ] **Step 1: Replace the single-shot scroll with a paging loop**

Replace the contents of `ScrollWithKeywordFilterAsync`'s `try` block with:

```csharp
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = fieldKey,
                            Match = new Match { Keyword = value }
                        }
                    }
                }
            };

            var results = new List<SearchResult>();
            PointId? offset = null;

            // A single scroll returns at most `limit` points. Left unpaged this silently truncated
            // every file over 100 chunks — real files in these collections run to 368 — so the graph
            // was rebuilt from a partial view with no error anywhere.
            while (true)
            {
                ScrollResponse response = await GetClient().ScrollAsync(
                    collectionName,
                    filter: filter,
                    limit: limit,
                    offset: offset,
                    cancellationToken: cancellationToken);

                results.AddRange(response.Result.Select(r => new SearchResult
                {
                    Score = 0f,
                    Chunk = BuildChunkFromPayload(r.Id.Uuid, r.Payload)
                }));

                if (response.NextPageOffset is null) break;
                offset = response.NextPageOffset;
            }

            return results;
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build Libraries/CodeAssist.Core/CodeAssist.Core.csproj`
Expected: Build succeeded, 0 errors.

If `ScrollResponse.NextPageOffset` does not exist on the client version in use, read the compiler error and use the equivalent member (the gRPC contract exposes it as `next_page_offset`). Do not fall back to a fixed limit.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: PASS, 15 tests.

- [ ] **Step 4: Commit**

```bash
git add Libraries/CodeAssist.Core/Services/QdrantService.cs
git commit -m "Page per-file chunk scrolls instead of silently stopping at 100"
```

---

### Task 5: Delete before upsert on the promotion path

This is the defect that actually produces the five-copies symptom, and it is the one the spec does not name. It fires on every save, regardless of separators or payload indexes.

**Files:**
- Create: `Libraries/CodeAssist.Core/Services/IQdrantWriter.cs`
- Modify: `Libraries/CodeAssist.Core/Services/QdrantService.cs` (declare it implements the interface)
- Modify: `Libraries/CodeAssist.Core/Caching/L2PromotionService.cs` (constructor + `ProcessBatchAsync` + test seam)
- Modify: `Libraries/CodeAssist.Core/Extensions/ServiceCollectionExtensions.cs`
- Create: `Libraries/CodeAssist.Core.Tests/Caching/FakeQdrantWriter.cs`
- Create: `Libraries/CodeAssist.Core.Tests/Caching/TestHotCache.cs`
- Create: `Libraries/CodeAssist.Core.Tests/Caching/L2PromotionOrderingTests.cs`

**Interfaces:**
- Consumes: `IndexPath.Normalize` (Task 1), `BuildRelativePathFilter` behavior (Task 3).
- Produces: `public interface CodeAssist.Core.Services.IQdrantWriter` with `Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)`, `Task DeleteByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default)`, `Task UpsertPointsAsync(string collectionName, IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points, CancellationToken cancellationToken = default)`. `L2PromotionService`'s constructor takes `IQdrantWriter` in place of `QdrantService`, and gains `internal Task PromoteNowAsync(CachedFile cachedFile, string collectionName)`.

- [ ] **Step 1: Confirm the current signatures you are about to abstract**

Run: `grep -n "public async Task<bool> CollectionExistsAsync\|public async Task DeleteByFilePathAsync\|public async Task UpsertPointsAsync" Libraries/CodeAssist.Core/Services/QdrantService.cs`
Expected: three matches. The interface below must match these signatures exactly — including default parameter values — or `QdrantService` will not satisfy it.

- [ ] **Step 2: Write the fake**

Create `Libraries/CodeAssist.Core.Tests/Caching/FakeQdrantWriter.cs`:

```csharp
using CodeAssist.Core.Services;

namespace CodeAssist.Core.Tests.Caching;

/// <summary>
/// Records the order of calls so a test can assert that a delete precedes the upsert for a file.
/// </summary>
internal sealed class FakeQdrantWriter : IQdrantWriter
{
    public List<string> Calls { get; } = [];
    public List<string> DeletedPaths { get; } = [];
    public int UpsertedPointCount { get; private set; }
    public bool CollectionExists { get; set; } = true;

    public Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"exists:{collectionName}");
        return Task.FromResult(CollectionExists);
    }

    public Task DeleteByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"delete:{relativePath}");
        DeletedPaths.Add(relativePath);
        return Task.CompletedTask;
    }

    public Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"upsert:{points.Count}");
        UpsertedPointCount += points.Count;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Write the HotCache test factory**

`L2PromotionService`'s constructor needs a `HotCache` only to subscribe to its promotion event, so nothing here contacts an embedding server. Create `Libraries/CodeAssist.Core.Tests/Caching/TestHotCache.cs`:

```csharp
using CodeAssist.Core.Caching;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Tests.Caching;

internal static class TestHotCache
{
    public static HotCache Create()
    {
        // OllamaService takes (IOptions, ILogger) — no HttpClient. Its constructor builds an
        // OllamaApiClient from options.OllamaUrl, which defaults to http://localhost:11435; nothing
        // here calls it, so no request is ever made.
        IOptions<CodeAssistOptions> options = Options.Create(new CodeAssistOptions());
        var ollama = new OllamaService(options, NullLogger<OllamaService>.Instance);
        return new HotCache(
            ollama,
            new ChunkerFactory(NullLoggerFactory.Instance),
            options,
            NullLogger<HotCache>.Instance);
    }
}
```

If the `ChunkerFactory` constructor differs, run `grep -n "public ChunkerFactory(" Libraries/CodeAssist.Core/Chunking/ChunkerFactory.cs` and match it exactly.

- [ ] **Step 4: Write the failing test**

Create `Libraries/CodeAssist.Core.Tests/Caching/L2PromotionOrderingTests.cs`:

```csharp
using CodeAssist.Core.Caching;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class L2PromotionOrderingTests
{
    private static CachedFile MakeCachedFile(string relativePath, int chunkCount)
    {
        var chunks = new List<CodeChunk>();
        var embeddings = new List<float[]>();
        for (var i = 0; i < chunkCount; i++)
        {
            chunks.Add(new CodeChunk
            {
                Id = Guid.NewGuid(),
                FilePath = @"C:\repo\" + relativePath.Replace('/', '\\'),
                RelativePath = relativePath,
                Content = $"chunk {i}",
                StartLine = i * 10,
                EndLine = i * 10 + 5,
                ChunkType = "method",
                Language = "csharp",
                ContentHash = $"hash{i}"
            });
            embeddings.Add([0.1f, 0.2f]);
        }

        return new CachedFile
        {
            FilePath = @"C:\repo\" + relativePath.Replace('/', '\\'),
            RelativePath = relativePath,
            RepositoryRoot = @"C:\repo",
            Content = "content",
            ContentHash = "filehash",
            Language = "csharp",
            Chunks = chunks,
            Embeddings = embeddings,
            LastModified = DateTime.UtcNow,
            CachedAt = DateTime.UtcNow
        };
    }

    private static L2PromotionService MakeService(FakeQdrantWriter writer, HotCache hotCache) =>
        new(hotCache,
            writer,
            Options.Create(new CodeAssistOptions { EnableL2Promotion = true }),
            NullLogger<L2PromotionService>.Instance);

    [Fact]
    public async Task PromotingAFile_DeletesItsPriorChunksBeforeUpserting()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 3), "myrepo");

        int deleteIndex = writer.Calls.IndexOf("delete:Editing/Foo.cs");
        int upsertIndex = writer.Calls.FindIndex(c => c.StartsWith("upsert:", StringComparison.Ordinal));

        Assert.True(deleteIndex >= 0, "the file's prior chunks must be deleted");
        Assert.True(upsertIndex >= 0, "the new chunks must be upserted");
        Assert.True(deleteIndex < upsertIndex, "the delete must precede the upsert, or the old copy survives");
    }

    [Fact]
    public async Task PromotingTheSameFileTwice_DeletesOncePerPromotion()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");
        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Equal(2, writer.DeletedPaths.Count(p => p == "Editing/Foo.cs"));
    }

    [Fact]
    public async Task PromotingTwoFiles_DeletesEachPathExactlyOnce()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");
        await service.PromoteNowAsync(MakeCachedFile("Editing/Bar.cs", 2), "myrepo");

        Assert.Equal(["Editing/Foo.cs", "Editing/Bar.cs"], writer.DeletedPaths);
    }

    [Fact]
    public async Task WhenTheCollectionIsMissing_NothingIsDeletedAndNothingIsUpserted()
    {
        var writer = new FakeQdrantWriter { CollectionExists = false };
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        await service.PromoteNowAsync(MakeCachedFile("Editing/Foo.cs", 2), "myrepo");

        Assert.Empty(writer.DeletedPaths);
        Assert.Equal(0, writer.UpsertedPointCount);
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter L2PromotionOrderingTests`
Expected: FAIL to compile — `IQdrantWriter` and `PromoteNowAsync` do not exist.

- [ ] **Step 6: Create the interface**

Create `Libraries/CodeAssist.Core/Services/IQdrantWriter.cs`:

```csharp
namespace CodeAssist.Core.Services;

/// <summary>
/// The narrow slice of Qdrant that the promotion path writes through.
/// </summary>
/// <remarks>
/// Exists so the delete-then-upsert ordering on the promotion path can be asserted without a live
/// Qdrant. That ordering is not cosmetic: chunk ids are freshly generated on every chunking run, so an
/// upsert can never overwrite the previous version of a file by id, and a promotion without a preceding
/// delete appends a complete additional copy of the file every time it is saved.
/// </remarks>
public interface IQdrantWriter
{
    Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default);

    Task DeleteByFilePathAsync(string collectionName, string relativePath, CancellationToken cancellationToken = default);

    Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<(Guid id, float[] vector, Dictionary<string, object> payload)> points,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 7: Declare `QdrantService` as an implementation**

In `Libraries/CodeAssist.Core/Services/QdrantService.cs`, change:

```csharp
public sealed class QdrantService
```

to:

```csharp
public sealed class QdrantService : IQdrantWriter
```

The three methods already match the interface; no bodies change.

- [ ] **Step 8: Register the interface in DI**

In `Libraries/CodeAssist.Core/Extensions/ServiceCollectionExtensions.cs`, after **each** of the two `services.AddSingleton<QdrantService>();` lines (~42 and ~81), add:

```csharp
            services.AddSingleton<IQdrantWriter>(sp => sp.GetRequiredService<QdrantService>());
```

- [ ] **Step 9: Switch `L2PromotionService` to the interface and add the delete**

In `Libraries/CodeAssist.Core/Caching/L2PromotionService.cs`, change the field:

```csharp
    private readonly IQdrantWriter _qdrantService;
```

and the constructor parameter:

```csharp
    public L2PromotionService(
        HotCache hotCache,
        IQdrantWriter qdrantService,
        IOptions<CodeAssistOptions> options,
        ILogger<L2PromotionService> logger)
```

Then in `ProcessBatchAsync`, immediately after the `CollectionExistsAsync` guard block and **before** the `var points = new List<...>();` line, insert:

```csharp
                // Remove each file's previous chunks before writing its new ones. Chunk ids are freshly
                // generated on every chunking run, so an upsert cannot overwrite the prior version by id;
                // without this delete each save appended another complete copy of the file, which is how
                // one method came to be returned five times at five different line ranges.
                foreach (string relativePath in group
                             .Select(t => IndexPath.Normalize(t.CachedFile.RelativePath))
                             .Distinct())
                {
                    await _qdrantService.DeleteByFilePathAsync(collectionName, relativePath);
                }
```

- [ ] **Step 10: Add the test seam**

Still in `L2PromotionService`, add next to `QueuePromotionAsync`:

```csharp
    /// <summary>
    /// Promote a file immediately rather than through the background queue.
    /// </summary>
    /// <remarks>
    /// A test seam, not API: the queue's timing makes the delete-then-upsert ordering impossible to
    /// observe reliably. Internal because <see cref="PromotionTask"/> is internal.
    /// </remarks>
    internal Task PromoteNowAsync(CachedFile cachedFile, string collectionName) =>
        ProcessBatchAsync([
            new PromotionTask
            {
                CachedFile = cachedFile,
                CollectionName = collectionName,
                QueuedAt = DateTime.UtcNow
            }
        ]);
```

- [ ] **Step 11: Run the tests to verify they pass**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: PASS, 19 tests.

- [ ] **Step 12: Commit**

```bash
git add Libraries/CodeAssist.Core/Services/IQdrantWriter.cs Libraries/CodeAssist.Core/Services/QdrantService.cs Libraries/CodeAssist.Core/Caching/L2PromotionService.cs Libraries/CodeAssist.Core/Extensions/ServiceCollectionExtensions.cs Libraries/CodeAssist.Core.Tests
git commit -m "Delete a file's old chunks before promoting its new ones"
```

---

### Task 6: Remove deleted and renamed files from the index

**Files:**
- Modify: `Libraries/CodeAssist.Core/Caching/L2PromotionService.cs` (add `RemoveFileAsync`)
- Modify: `Libraries/CodeAssist.Core/Caching/FileWatcherService.cs` (`OnFileDeleted`, `OnFileRenamed`)
- Create: `Libraries/CodeAssist.Core.Tests/Caching/L2PromotionRemovalTests.cs`

**Interfaces:**
- Consumes: `IQdrantWriter` and the `FakeQdrantWriter`/`TestHotCache` helpers from Task 5; `IndexPath.Normalize` from Task 1.
- Produces: `public Task RemoveFileAsync(string filePath, string repositoryRoot, CancellationToken cancellationToken = default)` on `L2PromotionService`.

- [ ] **Step 1: Read the existing collection-resolution helper**

Run: `grep -n "GetCollectionForFile" Libraries/CodeAssist.Core/Caching/L2PromotionService.cs`

`RemoveFileAsync` must resolve its collection the same way promotion does. Reuse `GetCollectionForFile` rather than writing a second resolver — the `CollectionNaming` remarks in this codebase document what happened the last time two resolvers disagreed. Note its exact return type (nullable or not) and match the guard below to it.

- [ ] **Step 2: Write the failing test**

Create `Libraries/CodeAssist.Core.Tests/Caching/L2PromotionRemovalTests.cs`:

```csharp
using CodeAssist.Core.Caching;
using CodeAssist.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class L2PromotionRemovalTests
{
    private static L2PromotionService MakeService(FakeQdrantWriter writer, HotCache hotCache) =>
        new(hotCache,
            writer,
            Options.Create(new CodeAssistOptions { EnableL2Promotion = true }),
            NullLogger<L2PromotionService>.Instance);

    [Fact]
    public async Task RemoveFileAsync_DeletesTheFilesChunksUsingTheNormalizedPath()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        string root = Path.Combine(Path.GetTempPath(), "myrepo");
        service.RegisterRepositoryCollection(root, "myrepo");

        await service.RemoveFileAsync(Path.Combine(root, "Editing", "Gone.cs"), root);

        Assert.Equal(["Editing/Gone.cs"], writer.DeletedPaths);
    }

    [Fact]
    public async Task RemoveFileAsync_DoesNothingWhenNoCollectionIsRegistered()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        string root = Path.Combine(Path.GetTempPath(), "unregistered");

        await service.RemoveFileAsync(Path.Combine(root, "Editing", "Gone.cs"), root);

        Assert.Empty(writer.DeletedPaths);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter L2PromotionRemovalTests`
Expected: FAIL to compile — `RemoveFileAsync` does not exist.

- [ ] **Step 4: Implement `RemoveFileAsync`**

Add to `L2PromotionService`, next to `QueuePromotionAsync`:

```csharp
    /// <summary>
    /// Remove a file's chunks from L2 after it is deleted or renamed on disk.
    /// </summary>
    /// <remarks>
    /// The watcher previously only evicted from L1 on delete, so a removed file's chunks stayed in Qdrant
    /// and kept being returned by searches until someone ran a full refresh. A stale hit with no newer
    /// copy to outrank it is worse than a duplicate.
    /// </remarks>
    public async Task RemoveFileAsync(
        string filePath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        string? collectionName = GetCollectionForFile(filePath, repositoryRoot);
        if (string.IsNullOrEmpty(collectionName))
        {
            _logger.LogDebug("No collection registered for {File}; nothing to remove from L2", filePath);
            return;
        }

        string relativePath = IndexPath.Normalize(Path.GetRelativePath(repositoryRoot, filePath));

        try
        {
            if (!await _qdrantService.CollectionExistsAsync(collectionName, cancellationToken)) return;

            await _qdrantService.DeleteByFilePathAsync(collectionName, relativePath, cancellationToken);
            _logger.LogInformation("Removed {File} from collection {Collection}", relativePath, collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove {File} from collection {Collection}", relativePath, collectionName);
        }
    }
```

If `GetCollectionForFile` returns a collection name for unregistered roots rather than null, the second test will fail — in that case make the guard check `_fileToCollection` directly for a registered root and keep both tests as written.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter L2PromotionRemovalTests`
Expected: PASS, 2 tests.

- [ ] **Step 6: Wire the watcher to it**

In `Libraries/CodeAssist.Core/Caching/FileWatcherService.cs`, add `L2PromotionService l2Promotion` to the primary constructor:

```csharp
public sealed class FileWatcherService(
    HotCache hotCache,
    L2PromotionService l2Promotion,
    IOptions<CodeAssistOptions> options,
    ILogger<FileWatcherService> logger)
    : IDisposable
```

There is no dependency cycle: `L2PromotionService` depends on `HotCache` and `IQdrantWriter`, not on `FileWatcherService`. Both are already singletons.

Replace `OnFileDeleted`:

```csharp
    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath)) return;

        logger.LogDebug("File deleted: {Path}", e.FullPath);
        hotCache.Remove(e.FullPath);
        RemoveFromL2(e.FullPath);
    }
```

and the removal half of `OnFileRenamed`:

```csharp
        // Remove old path
        if (!ShouldIgnoreFile(e.OldFullPath))
        {
            hotCache.Remove(e.OldFullPath);
            RemoveFromL2(e.OldFullPath);
        }
```

Then add the helper alongside the other private methods:

```csharp
    /// <summary>
    /// Fire-and-forget removal from L2. Watcher callbacks are synchronous void handlers, so the work is
    /// handed to the thread pool; RemoveFileAsync swallows and logs its own failures.
    /// </summary>
    private void RemoveFromL2(string filePath)
    {
        if (!_repositoryRoots.TryGetValue(filePath, out string? repositoryRoot)) return;

        _ = Task.Run(() => l2Promotion.RemoveFileAsync(filePath, repositoryRoot));
    }
```

- [ ] **Step 7: Confirm the repository root lookup actually resolves**

Run: `grep -n "_repositoryRoots" Libraries/CodeAssist.Core/Caching/FileWatcherService.cs`

`_repositoryRoots` is keyed by file path. If it is only populated on change (not on delete), a deleted file may have no entry and `RemoveFromL2` would silently no-op. If so, resolve the root the same way `DebouncedUpdate` does — match the established lookup rather than inventing a second one — and re-run the tests.

- [ ] **Step 8: Verify the library builds and all tests pass**

Run: `dotnet build Libraries/CodeAssist.Core/CodeAssist.Core.csproj && dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: Build succeeded; 21 tests PASS.

- [ ] **Step 9: Commit**

```bash
git add Libraries/CodeAssist.Core/Caching/L2PromotionService.cs Libraries/CodeAssist.Core/Caching/FileWatcherService.cs Libraries/CodeAssist.Core.Tests
git commit -m "Take deleted and renamed files out of the index instead of only out of L1"
```

---

### Task 7: Make lastUpdated and lastCommitSha mean "last update"

`IndexStateFile` is currently a private nested class inside `RepositoryIndexer`, so nothing else can touch it. Extract it into a store both writers share.

**Files:**
- Create: `Libraries/CodeAssist.Core/Models/IndexStateFile.cs`
- Create: `Libraries/CodeAssist.Core/Services/IndexStateStore.cs`
- Modify: `Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs` (remove the nested class and three private helpers; delegate to the store)
- Modify: `Libraries/CodeAssist.Core/Caching/L2PromotionService.cs` (touch state after a successful promotion)
- Modify: `Libraries/CodeAssist.Core/Extensions/ServiceCollectionExtensions.cs`
- Modify: `Libraries/CodeAssist.Core.Tests/Caching/L2PromotionOrderingTests.cs` and `L2PromotionRemovalTests.cs` (constructor change)
- Create: `Libraries/CodeAssist.Core.Tests/Services/IndexStateStoreTests.cs`

**Interfaces:**
- Consumes: `CollectionNaming.ForRepository`.
- Produces: `public sealed class CodeAssist.Core.Services.IndexStateStore` with `Task<IndexStateFile?> LoadAsync(string repositoryName, CancellationToken ct = default)`, `Task SaveAsync(string repositoryName, IndexStateFile state, CancellationToken ct = default)`, `Task TouchAsync(string collectionName, string? commitSha, CancellationToken ct = default)`, `void Delete(string repositoryName)`, `string GetStatePath(string repositoryName)`. `IndexStateFile` becomes a public record in `CodeAssist.Core.Models`.

- [ ] **Step 1: Write the failing test**

Create `Libraries/CodeAssist.Core.Tests/Services/IndexStateStoreTests.cs`:

```csharp
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class IndexStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "codeassist-state-" + Guid.NewGuid().ToString("N"));

    private IndexStateStore MakeStore() =>
        new(Options.Create(new CodeAssistOptions { IndexStateDirectory = _dir }),
            NullLogger<IndexStateStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static IndexStateFile MakeState(DateTimeOffset lastUpdated) => new()
    {
        RepositoryName = "MyRepo",
        RootPath = @"C:\repo",
        LastCommitSha = "aaaa111",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
        LastUpdatedAt = lastUpdated,
        EmbeddingModel = "bge-base-en-v1.5",
        CollectionName = "myrepo",
        IncludePatterns = ["*.cs"],
        ExcludePatterns = [],
        Files = []
    };

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        IndexStateStore store = MakeStore();

        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddHours(-1)));
        IndexStateFile? loaded = await store.LoadAsync("MyRepo");

        Assert.NotNull(loaded);
        Assert.Equal("myrepo", loaded.CollectionName);
        Assert.Equal("aaaa111", loaded.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_AdvancesLastUpdatedAt()
    {
        IndexStateStore store = MakeStore();
        DateTimeOffset stale = DateTimeOffset.UtcNow.AddDays(-2);
        await store.SaveAsync("MyRepo", MakeState(stale));

        await store.TouchAsync("myrepo", commitSha: null);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo");
        Assert.NotNull(loaded);
        Assert.True(loaded.LastUpdatedAt > stale, "a promotion is an update and must advance lastUpdated");
    }

    [Fact]
    public async Task TouchAsync_UpdatesCommitShaWhenGiven()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-2)));

        await store.TouchAsync("myrepo", commitSha: "bbbb222");

        IndexStateFile? loaded = await store.LoadAsync("MyRepo");
        Assert.Equal("bbbb222", loaded!.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_LeavesCommitShaAloneWhenNotGiven()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("MyRepo", MakeState(DateTimeOffset.UtcNow.AddDays(-2)));

        await store.TouchAsync("myrepo", commitSha: null);

        IndexStateFile? loaded = await store.LoadAsync("MyRepo");
        Assert.Equal("aaaa111", loaded!.LastCommitSha);
    }

    [Fact]
    public async Task TouchAsync_IsSilentWhenNoStateFileExists()
    {
        IndexStateStore store = MakeStore();

        await store.TouchAsync("neverindexed", commitSha: null);

        Assert.Null(await store.LoadAsync("neverindexed"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter IndexStateStoreTests`
Expected: FAIL to compile — `IndexStateStore` and a public `IndexStateFile` do not exist.

- [ ] **Step 3: Extract `IndexStateFile`**

Create `Libraries/CodeAssist.Core/Models/IndexStateFile.cs`:

```csharp
namespace CodeAssist.Core.Models;

/// <summary>
/// The on-disk index state for one repository.
/// </summary>
/// <remarks>
/// Public rather than nested-private because more than one writer updates an index: the full indexer
/// rewrites it wholesale, and the promotion path advances its freshness stamps.
/// </remarks>
public sealed record IndexStateFile
{
    public required string RepositoryName { get; init; }
    public required string RootPath { get; init; }
    public string? LastCommitSha { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastUpdatedAt { get; init; }
    public required string EmbeddingModel { get; init; }
    public required string CollectionName { get; init; }
    public required List<string> IncludePatterns { get; init; }
    public required List<string> ExcludePatterns { get; init; }
    public required Dictionary<string, IndexedFile> Files { get; init; }
}
```

Then delete the `private sealed class IndexStateFile { ... }` block at the bottom of `RepositoryIndexer.cs`, and add `using CodeAssist.Core.Models;` there if not already present.

- [ ] **Step 4: Create the store**

Create `Libraries/CodeAssist.Core/Services/IndexStateStore.cs`:

```csharp
using System.Text.Json;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Services;

/// <summary>
/// Reads and writes the per-repository index state file.
/// </summary>
/// <remarks>
/// The state file's name is <c>CollectionNaming.ForRepository(repositoryName) + ".json"</c>, which is the
/// same string as the Qdrant collection name — that is what lets the promotion path find the right state
/// file knowing only the collection it just wrote to.
///
/// <para><c>lastUpdated</c> and <c>lastCommitSha</c> previously advanced only on a manual refresh, so they
/// meant "last manual refresh" while being read as "last update". A repository could report a stamp two
/// days and many commits stale while its index held files written that morning.</para>
/// </remarks>
public sealed class IndexStateStore(
    IOptions<CodeAssistOptions> options,
    ILogger<IndexStateStore> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CodeAssistOptions _options = options.Value;

    public string GetStatePath(string repositoryName) =>
        Path.Combine(_options.IndexStateDirectory, $"{CollectionNaming.ForRepository(repositoryName)}.json");

    public async Task<IndexStateFile?> LoadAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);
        if (!File.Exists(path)) return null;

        try
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<IndexStateFile>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read index state at {Path}", path);
            return null;
        }
    }

    public async Task SaveAsync(string repositoryName, IndexStateFile state, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(state, SerializerOptions), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Advance the freshness stamps for an already-indexed repository, identified by its collection name.
    /// Does nothing if the repository has no state file — a promotion is not an index.
    /// </summary>
    public async Task TouchAsync(string collectionName, string? commitSha, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            string path = Path.Combine(_options.IndexStateDirectory, $"{collectionName}.json");
            if (!File.Exists(path)) return;

            string json = await File.ReadAllTextAsync(path, cancellationToken);
            IndexStateFile? state = JsonSerializer.Deserialize<IndexStateFile>(json);
            if (state is null) return;

            IndexStateFile updated = state with
            {
                LastUpdatedAt = DateTimeOffset.UtcNow,
                LastCommitSha = commitSha ?? state.LastCommitSha
            };

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(updated, SerializerOptions), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to touch index state for collection {Collection}", collectionName);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Delete(string repositoryName)
    {
        string path = GetStatePath(repositoryName);
        if (File.Exists(path)) File.Delete(path);
    }
}
```

- [ ] **Step 5: Run the store tests to verify they pass**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter IndexStateStoreTests`
Expected: PASS, 5 tests.

- [ ] **Step 6: Delegate `RepositoryIndexer` to the store**

Add `IndexStateStore indexStateStore` to `RepositoryIndexer`'s primary constructor, immediately before `IOptions<CodeAssistOptions> options`, so it reads:

```csharp
public sealed class RepositoryIndexer(
    OllamaService ollamaService,
    QdrantService qdrantService,
    ChunkerFactory chunkerFactory,
    IndexStateStore indexStateStore,
    IOptions<CodeAssistOptions> options,
    ILogger<RepositoryIndexer> logger)
```

Then replace the call sites:

- ~line 68: `IndexStateFile? existingState = await indexStateStore.LoadAsync(repositoryName, cancellationToken);`
- ~line 248: `await indexStateStore.SaveAsync(repositoryName, newState, cancellationToken);`
- ~line 328: `IndexStateFile? stateFile = await indexStateStore.LoadAsync(repositoryName, cancellationToken);`
- ~line 370, in the delete path, replace the `statePath` / `File.Exists` / `File.Delete` block with: `indexStateStore.Delete(repositoryName);`

Delete the three now-unused private methods (`GetIndexStatePath`, `LoadIndexStateAsync`, `SaveIndexStateAsync`).

- [ ] **Step 7: Register the store in DI**

In `Libraries/CodeAssist.Core/Extensions/ServiceCollectionExtensions.cs`, before **each** of the two `services.AddSingleton<RepositoryIndexer>();` lines, add:

```csharp
            services.AddSingleton<IndexStateStore>();
```

- [ ] **Step 8: Touch state on successful promotion**

In `L2PromotionService`, add `IndexStateStore indexStateStore` as the third constructor parameter and store it in a `private readonly IndexStateStore _indexStateStore;` field. Then in `ProcessBatchAsync`, immediately after the successful-upsert log line, add:

```csharp
                // A promotion is an update. Without this, lastUpdated meant "last manual refresh" and
                // anything reading it as a freshness signal was wrong.
                await _indexStateStore.TouchAsync(collectionName, commitSha: null);
```

- [ ] **Step 9: Update the two promotion test files for the new constructor**

In both `L2PromotionOrderingTests.cs` and `L2PromotionRemovalTests.cs`, replace `MakeService` with:

```csharp
    private static L2PromotionService MakeService(FakeQdrantWriter writer, HotCache hotCache) =>
        new(hotCache,
            writer,
            new IndexStateStore(
                Options.Create(new CodeAssistOptions
                {
                    IndexStateDirectory = Path.Combine(
                        Path.GetTempPath(), "codeassist-test-state-" + Guid.NewGuid().ToString("N"))
                }),
                NullLogger<IndexStateStore>.Instance),
            Options.Create(new CodeAssistOptions { EnableL2Promotion = true }),
            NullLogger<L2PromotionService>.Instance);
```

Add `using CodeAssist.Core.Services;` to both files. `TouchAsync` returns silently when no state file exists, so these tests need nothing on disk and create no directories.

- [ ] **Step 10: Run the full suite**

Run: `dotnet build Libraries/CodeAssist.Core/CodeAssist.Core.csproj && dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: Build succeeded; 26 tests PASS.

- [ ] **Step 11: Commit**

```bash
git add Libraries/CodeAssist.Core/Models/IndexStateFile.cs Libraries/CodeAssist.Core/Services/IndexStateStore.cs Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs Libraries/CodeAssist.Core/Caching/L2PromotionService.cs Libraries/CodeAssist.Core/Extensions/ServiceCollectionExtensions.cs Libraries/CodeAssist.Core.Tests
git commit -m "Give index state one owner, and let promotion advance the freshness stamps"
```

---

### Task 8: The regression test the spec asks for

The spec is explicit that this must run through **both** writers, because they disagreed and a test exercising only one would have passed throughout. It needs a live Qdrant and a live embedding server, so it is gated on an environment variable and skipped by default.

**Files:**
- Create: `Libraries/CodeAssist.Core.Tests/Integration/RequiresLiveServicesFactAttribute.cs`
- Create: `Libraries/CodeAssist.Core.Tests/Integration/IndexDuplicationRegressionTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-7. The fourth test covers Task 4's paging fix, which has no unit coverage of its own.
- Produces: nothing consumed by other tasks.

- [ ] **Step 1: Write the skip attribute**

Create `Libraries/CodeAssist.Core.Tests/Integration/RequiresLiveServicesFactAttribute.cs`:

```csharp
using Xunit;

namespace CodeAssist.Core.Tests.Integration;

/// <summary>
/// A fact that runs only when CODEASSIST_TEST_QDRANT_URL is set. These tests need a real Qdrant and a
/// real embedding server; they are the only way to catch the duplication bug end to end, because the
/// defect lives in the interaction between two writers rather than inside either one.
/// </summary>
public sealed class RequiresLiveServicesFactAttribute : FactAttribute
{
    public RequiresLiveServicesFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEASSIST_TEST_QDRANT_URL")))
        {
            Skip = "Set CODEASSIST_TEST_QDRANT_URL and CODEASSIST_TEST_OLLAMA_URL to run live-service tests.";
        }
    }
}
```

- [ ] **Step 2: Confirm the constructor signatures the test will call**

Run: `grep -n "public QdrantService(\|public OllamaService(" Libraries/CodeAssist.Core/Services/*.cs && sed -n '19,34p' Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs`

As of writing, these are:

- `QdrantService(IOptions<CodeAssistOptions>, ILogger<QdrantService>)`
- `OllamaService(IOptions<CodeAssistOptions>, ILogger<OllamaService>)` — **no `HttpClient` parameter**
- `RepositoryIndexer(OllamaService, QdrantService, ChunkerFactory, IOptions<CodeAssistOptions>, ILogger<RepositoryIndexer>)` — note ollama comes **first**, before qdrant — with `IndexStateStore` inserted before `IOptions` by Task 7
- `L2PromotionService(HotCache, IQdrantWriter, IndexStateStore, IOptions<CodeAssistOptions>, ILogger<L2PromotionService>)` after Tasks 5 and 7

The test below is written against exactly these. If the grep disagrees, the grep wins.

- [ ] **Step 3: Write the regression test**

Create `Libraries/CodeAssist.Core.Tests/Integration/IndexDuplicationRegressionTests.cs`:

```csharp
using System.Text;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Integration;

public class IndexDuplicationRegressionTests : IAsyncLifetime
{
    private readonly string _repoRoot = Path.Combine(Path.GetTempPath(), "codeassist-dup-" + Guid.NewGuid().ToString("N"));
    private readonly string _repoName = "duptest" + Guid.NewGuid().ToString("N")[..8];
    private QdrantService _qdrant = null!;
    private RepositoryIndexer _indexer = null!;
    private HotCache _hotCache = null!;
    private L2PromotionService _promotion = null!;
    private string _collection = null!;

    private const string Version1 = """
        namespace Sample;
        public class Widget
        {
            public int Measure() { return 1; }
        }
        """;

    private const string Version2 = """
        namespace Sample;
        public class Widget
        {
            // an added line shifts every line number below it
            public int Measure() { return 42; }
        }
        """;

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_repoRoot, "Src"));
        File.WriteAllText(Path.Combine(_repoRoot, "Src", "Widget.cs"), Version1);

        var options = Options.Create(new CodeAssistOptions
        {
            QdrantUrl = Environment.GetEnvironmentVariable("CODEASSIST_TEST_QDRANT_URL")!,
            OllamaUrl = Environment.GetEnvironmentVariable("CODEASSIST_TEST_OLLAMA_URL")!,
            IndexStateDirectory = Path.Combine(_repoRoot, ".state"),
            EnableL2Promotion = true
        });

        _collection = CollectionNaming.ForRepository(_repoName);
        _qdrant = new QdrantService(options, NullLogger<QdrantService>.Instance);
        var ollama = new OllamaService(options, NullLogger<OllamaService>.Instance);
        var chunkers = new ChunkerFactory(NullLoggerFactory.Instance);
        var stateStore = new IndexStateStore(options, NullLogger<IndexStateStore>.Instance);

        // Parameter order matters: RepositoryIndexer takes ollama BEFORE qdrant.
        _indexer = new RepositoryIndexer(ollama, _qdrant, chunkers, stateStore, options, NullLogger<RepositoryIndexer>.Instance);
        _hotCache = new HotCache(ollama, chunkers, options, NullLogger<HotCache>.Instance);
        _promotion = new L2PromotionService(_hotCache, _qdrant, stateStore, options, NullLogger<L2PromotionService>.Instance);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _qdrant.DeleteCollectionAsync(_collection); } catch { /* best effort */ }
        _promotion.Dispose();
        _hotCache.Dispose();
        if (Directory.Exists(_repoRoot)) Directory.Delete(_repoRoot, recursive: true);
    }

    private async Task<int> ChunkCountForAsync(string relativePath) =>
        (await _qdrant.SearchByFilePathAsync(_collection, relativePath)).Count;

    private async Task<List<string>> ContentsForAsync(string relativePath) =>
        (await _qdrant.SearchByFilePathAsync(_collection, relativePath))
        .Select(r => r.Chunk.Content).ToList();

    [RequiresLiveServicesFact]
    public async Task ReIndexingAModifiedFile_DoesNotLeaveTheOldVersionBehind()
    {
        string file = Path.Combine(_repoRoot, "Src", "Widget.cs");

        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);
        int before = await ChunkCountForAsync("Src/Widget.cs");
        Assert.True(before > 0, "first index produced no chunks — the test cannot detect duplication");

        await File.WriteAllTextAsync(file, Version2);
        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);

        Assert.Equal(before, await ChunkCountForAsync("Src/Widget.cs"));

        List<string> contents = await ContentsForAsync("Src/Widget.cs");
        Assert.DoesNotContain(contents, c => c.Contains("return 1;", StringComparison.Ordinal));
        Assert.Contains(contents, c => c.Contains("return 42;", StringComparison.Ordinal));
    }

    [RequiresLiveServicesFact]
    public async Task PromotingAModifiedFile_DoesNotLeaveTheOldVersionBehind()
    {
        string file = Path.Combine(_repoRoot, "Src", "Widget.cs");

        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);
        int before = await ChunkCountForAsync("Src/Widget.cs");
        Assert.True(before > 0, "first index produced no chunks — the test cannot detect duplication");

        _promotion.RegisterRepositoryCollection(_repoRoot, _collection);
        await File.WriteAllTextAsync(file, Version2);

        CachedFile? cached = await _hotCache.UpdateFileAsync(file, _repoRoot);
        Assert.NotNull(cached);
        await _promotion.PromoteNowAsync(cached, _collection);

        Assert.Equal(cached.Chunks.Count, await ChunkCountForAsync("Src/Widget.cs"));

        List<string> contents = await ContentsForAsync("Src/Widget.cs");
        Assert.DoesNotContain(contents, c => c.Contains("return 1;", StringComparison.Ordinal));
        Assert.Contains(contents, c => c.Contains("return 42;", StringComparison.Ordinal));
    }

    [RequiresLiveServicesFact]
    public async Task BothWritersUseTheSameRelativePathKey()
    {
        // If the two forms diverge again, one writer's rows become invisible to the other's delete
        // and duplication returns silently.
        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);
        _promotion.RegisterRepositoryCollection(_repoRoot, _collection);

        string file = Path.Combine(_repoRoot, "Src", "Widget.cs");
        CachedFile? cached = await _hotCache.UpdateFileAsync(file, _repoRoot);

        Assert.NotNull(cached);
        Assert.Equal("Src/Widget.cs", cached.RelativePath);
        Assert.All(cached.Chunks, c => Assert.Equal("Src/Widget.cs", c.RelativePath));
    }

    [RequiresLiveServicesFact]
    public async Task AFileWithMoreThanOnePageOfChunks_IsReadBackInFull()
    {
        // Covers Task 4. A scroll returns at most 100 points per page, and files well past that exist
        // in real collections — NetworkingDtos.cs holds 368 — so an unpaged read silently truncated
        // them and the graph was rebuilt from a partial file with no error anywhere.
        var wide = new StringBuilder("namespace Sample;\npublic class Wide\n{\n");
        for (var i = 0; i < 150; i++)
        {
            wide.AppendLine($"    public int Method{i}() {{ return {i}; }}");
        }

        wide.AppendLine("}");

        Directory.CreateDirectory(Path.Combine(_repoRoot, "Wide"));
        await File.WriteAllTextAsync(Path.Combine(_repoRoot, "Wide", "Wide.cs"), wide.ToString());

        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);

        int count = await ChunkCountForAsync("Wide/Wide.cs");

        Assert.True(
            count > 100,
            $"expected more than one page of chunks, got {count} — either the scroll is still "
            + "truncating at 100, or the chunker produced fewer than 100 chunks and this test no "
            + "longer proves anything");
    }
}
```

`PromoteNowAsync` is `internal`; `InternalsVisibleTo` from Task 1 makes it reachable here.

- [ ] **Step 4: Verify the tests skip cleanly with no services configured**

Run: `dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`
Expected: 26 passed, 4 skipped, 0 failed.

- [ ] **Step 5: Run them against live services**

```bash
CODEASSIST_TEST_QDRANT_URL=http://192.168.0.170:6333 \
CODEASSIST_TEST_OLLAMA_URL=http://192.168.0.170:11435 \
dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter IndexDuplicationRegressionTests
```

Expected: 4 PASS. These tests create and drop their own throwaway collection; they never touch `pdflibrary`, `pellucid`, or `mcpservers`.

- [ ] **Step 6: Confirm the test actually catches the bug**

This is the step that proves the test is worth having. Temporarily comment out the delete loop added in Task 5 Step 9 (`foreach (string relativePath in group...)`) and re-run:

```bash
CODEASSIST_TEST_QDRANT_URL=http://192.168.0.170:6333 \
CODEASSIST_TEST_OLLAMA_URL=http://192.168.0.170:11435 \
dotnet test Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj --filter PromotingAModifiedFile
```

Expected: FAIL — the chunk count roughly doubles and `return 1;` is still present. **Restore the delete loop** and re-run to confirm PASS before committing.

- [ ] **Step 7: Commit**

```bash
git add Libraries/CodeAssist.Core.Tests/Integration
git commit -m "Cover the duplication bug through both writers, since they were what disagreed"
```

---

## Follow-up, deliberately out of scope

Do not do these as part of this plan:

1. **One-off reindex of `pellucid` and `mcpservers`.** `refresh_index` only adds; clearing accumulated duplicates needs `delete_index` + `index_repository`, roughly 15 minutes per repository. Run it only after the above ships, or the rebuilt indexes start re-accumulating immediately. `pdflibrary` was already rebuilt clean on 2026-08-24.
2. **Amending `INDEX-DUPLICATION-DIAGNOSIS.md`** with the two corrections in "Verified findings" above, so the record matches what was measured.
3. **Casing.** Normalization deliberately preserves case. If Windows path casing ever diverges between the watcher and the globber, the same split returns in a form these tests will not catch.
