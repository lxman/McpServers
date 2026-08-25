using CodeAssist.Core.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Configuration;

public class CodeAssistOptionsValidatorTests
{
    private readonly CodeAssistOptionsValidator _validator = new();

    [Fact]
    public void Validate_AcceptsBuiltInDefaults()
    {
        ValidateOptionsResult result = _validator.Validate(null, new CodeAssistOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ReportsAllInvalidCoreSettings()
    {
        var options = new CodeAssistOptions
        {
            OllamaUrl = "not-a-url",
            QdrantUrl = "ftp://example.com",
            EmbeddingModel = " ",
            VectorDimension = 0,
            MaxChunkSize = 100,
            ChunkOverlap = 100,
            DefaultIncludePatterns = [],
            MinSimilarityScore = 2,
            L2PromotionBatchSize = 0,
            L2PromotionQueueCapacity = 0
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        string failures = string.Join(" | ", result.Failures);
        Assert.Contains("OllamaUrl", failures);
        Assert.Contains("QdrantUrl", failures);
        Assert.Contains("EmbeddingModel", failures);
        Assert.Contains("VectorDimension", failures);
        Assert.Contains("ChunkOverlap", failures);
        Assert.Contains("DefaultIncludePatterns", failures);
        Assert.Contains("MinSimilarityScore", failures);
        Assert.Contains("L2PromotionBatchSize", failures);
        Assert.Contains("L2PromotionQueueCapacity", failures);
    }
}
