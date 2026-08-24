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

        // ChunkerFactory takes the two chunker implementations directly, not a logger factory. Neither
        // is invoked here: PromoteNowAsync operates on an already-built CachedFile, so HotCache never
        // has to chunk anything for these tests.
        var treeSitterChunker = new TreeSitterChunker(options, NullLogger<TreeSitterChunker>.Instance);
        var defaultChunker = new DefaultChunker(options, NullLogger<DefaultChunker>.Instance);
        var chunkerFactory = new ChunkerFactory(treeSitterChunker, defaultChunker);

        return new HotCache(
            ollama,
            chunkerFactory,
            options,
            NullLogger<HotCache>.Instance);
    }
}
