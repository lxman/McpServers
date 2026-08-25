using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class QdrantServiceSymbolTests
{
    [Fact]
    public void FindMissingPayloadIndexes_ReportsEveryRequiredGap()
    {
        string[] existing = QdrantService.RequiredPayloadIndexFields
            .Where(field => field is not "relative_path" and not "calls_out_names")
            .ToArray();

        List<string> missing = QdrantService.FindMissingPayloadIndexes(existing);

        Assert.Equal(["calls_out_names", "relative_path"], missing);
    }

    [Fact]
    public void MatchesRequestedSymbol_DoesNotTreatOrdinaryClassMembersAsTheClass()
    {
        CodeChunk method = MakeChunk("RunAsync", "method", "Worker");

        Assert.False(QdrantService.MatchesRequestedSymbol(method, new HashSet<string> { "Worker" }));
    }

    [Fact]
    public void MatchesRequestedSymbol_MatchesCanonicalSplitSymbol()
    {
        CodeChunk splitMethod = MakeChunk("RunAsync (part 2)", "method_part2", "Worker");

        Assert.True(QdrantService.MatchesRequestedSymbol(splitMethod, new HashSet<string> { "RunAsync" }));
    }

    [Fact]
    public void MergeRequestedSymbolMatches_KeepsLegacySplitDefinitionBesideExactConstructor()
    {
        CodeChunk constructor = MakeChunk("Worker", "constructor", "Worker");
        CodeChunk splitClass = MakeChunk("Worker (part 1)", "class_part1", "Worker");
        CodeChunk unrelatedMember = MakeChunk("RunAsync", "method", "Worker");

        List<SearchResult> results = QdrantService.MergeRequestedSymbolMatches(
            [MakeResult(constructor)],
            [MakeResult(splitClass), MakeResult(unrelatedMember)],
            new HashSet<string> { "Worker" });

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Chunk.Id == constructor.Id);
        Assert.Contains(results, result => result.Chunk.Id == splitClass.Id);
        Assert.DoesNotContain(results, result => result.Chunk.Id == unrelatedMember.Id);
    }

    [Fact]
    public void MatchesRequestedQualifiedName_CanonicalizesSplitSuffix()
    {
        CodeChunk splitMethod = MakeChunk("RunAsync (part 2)", "method_part2", "Worker") with
        {
            QualifiedName = "Test.Worker.RunAsync (part 2)"
        };

        Assert.True(QdrantService.MatchesRequestedQualifiedName(
            splitMethod, new HashSet<string> { "Test.Worker.RunAsync" }));
    }

    private static SearchResult MakeResult(CodeChunk chunk) => new() { Chunk = chunk, Score = 0f };

    private static CodeChunk MakeChunk(string symbolName, string chunkType, string parentSymbol)
    {
        return new CodeChunk
        {
            Id = Guid.NewGuid(),
            FilePath = "Worker.cs",
            RelativePath = "Worker.cs",
            Content = symbolName,
            StartLine = 1,
            EndLine = 10,
            ChunkType = chunkType,
            SymbolName = symbolName,
            ParentSymbol = parentSymbol,
            Language = "csharp",
            ContentHash = Guid.NewGuid().ToString("N")
        };
    }
}
