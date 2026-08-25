using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public sealed class CollectionWriteCoordinatorTests
{
    [Fact]
    public async Task SameCollection_WaitsForTheCurrentWriter()
    {
        var coordinator = new CollectionWriteCoordinator();
        IAsyncDisposable first = await coordinator.AcquireAsync(
            "repo", TestContext.Current.CancellationToken);

        ValueTask<IAsyncDisposable> secondAttempt = coordinator.AcquireAsync(
            "repo", TestContext.Current.CancellationToken);

        Assert.False(secondAttempt.IsCompleted);
        await first.DisposeAsync();
        await using IAsyncDisposable second = await secondAttempt;
    }

    [Fact]
    public async Task DifferentCollections_DoNotBlockEachOther()
    {
        var coordinator = new CollectionWriteCoordinator();
        await using IAsyncDisposable first = await coordinator.AcquireAsync(
            "repo-a", TestContext.Current.CancellationToken);

        ValueTask<IAsyncDisposable> secondAttempt = coordinator.AcquireAsync(
            "repo-b", TestContext.Current.CancellationToken);

        Assert.True(secondAttempt.IsCompletedSuccessfully);
        await using IAsyncDisposable second = await secondAttempt;
    }
}
