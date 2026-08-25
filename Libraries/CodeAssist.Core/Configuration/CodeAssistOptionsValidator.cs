using Microsoft.Extensions.Options;

namespace CodeAssist.Core.Configuration;

public sealed class CodeAssistOptionsValidator : IValidateOptions<CodeAssistOptions>
{
    public ValidateOptionsResult Validate(string? name, CodeAssistOptions options)
    {
        var failures = new List<string>();

        ValidateHttpUrl(options.OllamaUrl, nameof(options.OllamaUrl), failures);
        ValidateHttpUrl(options.QdrantUrl, nameof(options.QdrantUrl), failures);

        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
            failures.Add("EmbeddingModel must not be empty.");
        if (options.VectorDimension <= 0)
            failures.Add("VectorDimension must be greater than zero.");
        if (options.MaxChunkSize <= 0)
            failures.Add("MaxChunkSize must be greater than zero.");
        if (options.ChunkOverlap < 0 || options.ChunkOverlap >= options.MaxChunkSize)
            failures.Add("ChunkOverlap must be non-negative and smaller than MaxChunkSize.");
        if (options.DefaultIncludePatterns.Count == 0)
            failures.Add("DefaultIncludePatterns must contain at least one pattern.");
        if (options.DefaultSearchLimit <= 0)
            failures.Add("DefaultSearchLimit must be greater than zero.");
        if (options.MinSimilarityScore is < 0 or > 1)
            failures.Add("MinSimilarityScore must be between 0 and 1.");
        if (string.IsNullOrWhiteSpace(options.IndexStateDirectory))
            failures.Add("IndexStateDirectory must not be empty.");
        if (options.HotCacheMaxFiles <= 0)
            failures.Add("HotCacheMaxFiles must be greater than zero.");
        if (options.HotCacheTtl <= TimeSpan.Zero)
            failures.Add("HotCacheTtl must be greater than zero.");
        if (options.FileWatcherDebounceDelay < TimeSpan.Zero)
            failures.Add("FileWatcherDebounceDelay must not be negative.");
        if (options.L2PromotionBatchSize <= 0)
            failures.Add("L2PromotionBatchSize must be greater than zero.");
        if (options.L2PromotionQueueCapacity <= 0)
            failures.Add("L2PromotionQueueCapacity must be greater than zero.");
        if (options.L2PromotionDelay < TimeSpan.Zero)
            failures.Add("L2PromotionDelay must not be negative.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateHttpUrl(string value, string propertyName, List<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https"))
        {
            failures.Add($"{propertyName} must be an absolute HTTP or HTTPS URL.");
        }
    }
}
