using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class EmbeddingCompatibilityTests
{
    [Fact]
    public void ValidateEmbeddingCompatibility_AcceptsMatchingIndexMetadata()
    {
        IndexStateFile state = MakeState("bge-base-en-v1.5", 768);

        RepositoryIndexer.ValidateEmbeddingCompatibility(state, "bge-base-en-v1.5", 768, 768);
    }

    [Fact]
    public void ValidateEmbeddingCompatibility_RejectsModelDrift()
    {
        IndexStateFile state = MakeState("nomic-embed-text", 768);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepositoryIndexer.ValidateEmbeddingCompatibility(state, "bge-base-en-v1.5", 768, 768));

        Assert.Contains("Delete and rebuild", exception.Message);
        Assert.Contains("nomic-embed-text", exception.Message);
        Assert.Contains("bge-base-en-v1.5", exception.Message);
    }

    [Fact]
    public void ValidateEmbeddingCompatibility_RejectsConfiguredDimensionMismatch()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepositoryIndexer.ValidateEmbeddingCompatibility(null, "model", 1024, 768));

        Assert.Contains("returned 1024-dimension", exception.Message);
        Assert.Contains("configured as 768", exception.Message);
    }

    [Fact]
    public void ValidateEmbeddingCompatibility_RejectsStoredDimensionMismatch()
    {
        IndexStateFile state = MakeState("model", 384);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RepositoryIndexer.ValidateEmbeddingCompatibility(state, "model", 768, 768));

        Assert.Contains("contains 384-dimension", exception.Message);
        Assert.Contains("returns 768", exception.Message);
    }

    [Fact]
    public void ValidateEmbeddingCompatibility_AcceptsLegacyStateWithoutDimension()
    {
        IndexStateFile state = MakeState("model", null);

        RepositoryIndexer.ValidateEmbeddingCompatibility(state, "model", 768, 768);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(769)]
    public void ValidateEmbeddingDimension_RejectsUnexpectedLengths(int actualDimension)
    {
        float[] embedding = new float[actualDimension];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            OllamaService.ValidateEmbeddingDimension(embedding, 768));

        Assert.Contains($"returned {actualDimension}-dimension", exception.Message);
    }

    private static IndexStateFile MakeState(string model, int? vectorDimension) => new()
    {
        RepositoryName = "repo",
        RootPath = @"C:\repo",
        CreatedAt = DateTimeOffset.UtcNow,
        LastUpdatedAt = DateTimeOffset.UtcNow,
        EmbeddingModel = model,
        VectorDimension = vectorDimension,
        CollectionName = "repo",
        IncludePatterns = ["*.cs"],
        ExcludePatterns = [],
        Files = []
    };
}
