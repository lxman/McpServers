using CodeAssist.Core.Services;
using Xunit;

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

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Normalize_PassesThroughEmptyInput(string? input)
    {
        Assert.Equal(input, IndexPath.Normalize(input!));
    }
}
