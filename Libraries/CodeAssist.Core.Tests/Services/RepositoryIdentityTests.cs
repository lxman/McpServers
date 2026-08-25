using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class RepositoryIdentityTests
{
    [Fact]
    public void ValidateRepositoryRoot_AcceptsTheOriginalRoot()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
        IndexStateFile state = MakeState(root);

        RepositoryIndexer.ValidateRepositoryRoot(state, root + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void ValidateRepositoryRoot_RejectsReuseForAnotherRoot()
    {
        string firstRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "first"));
        string secondRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "second"));
        IndexStateFile state = MakeState(firstRoot);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepositoryIndexer.ValidateRepositoryRoot(state, secondRoot));

        Assert.Contains("already assigned", exception.Message);
        Assert.Contains("distinct repository name", exception.Message);
    }

    private static IndexStateFile MakeState(string rootPath) => new()
    {
        RepositoryName = "repo",
        RootPath = rootPath,
        CreatedAt = DateTimeOffset.UtcNow,
        LastUpdatedAt = DateTimeOffset.UtcNow,
        EmbeddingModel = "model",
        VectorDimension = 768,
        CollectionName = "repo",
        IncludePatterns = ["*.cs"],
        ExcludePatterns = [],
        Files = []
    };
}
