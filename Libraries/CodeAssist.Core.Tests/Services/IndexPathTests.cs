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
