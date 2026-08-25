using CodeAssist.Core.Caching;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class L2PromotionRemovalTests
{
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
            new CollectionWriteCoordinator(),
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

        await service.RemoveFileAsync(Path.Combine(root, "Editing", "Gone.cs"), root,
            TestContext.Current.CancellationToken);

        Assert.Equal(["Editing/Gone.cs"], writer.DeletedPaths);
    }

    [Fact]
    public async Task RemoveFileAsync_DoesNothingWhenNoCollectionIsRegistered()
    {
        var writer = new FakeQdrantWriter();
        using HotCache hotCache = TestHotCache.Create();
        using L2PromotionService service = MakeService(writer, hotCache);

        string root = Path.Combine(Path.GetTempPath(), "unregistered");

        await service.RemoveFileAsync(Path.Combine(root, "Editing", "Gone.cs"), root,
            TestContext.Current.CancellationToken);

        Assert.Empty(writer.DeletedPaths);
    }
}
