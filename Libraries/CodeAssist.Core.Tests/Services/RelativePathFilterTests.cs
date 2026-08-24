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
