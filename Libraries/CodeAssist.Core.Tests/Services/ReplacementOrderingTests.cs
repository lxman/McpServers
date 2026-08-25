using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class ReplacementOrderingTests
{
    [Fact]
    public async Task StoreBeforeRetiringAsync_StoresEveryReplacementBeforeDeletingOldPoints()
    {
        var events = new List<string>();
        IReadOnlyList<Guid>[] oldPointSets = [[Guid.NewGuid()], [Guid.NewGuid()]];

        await RepositoryIndexer.StoreBeforeRetiringAsync(
            _ =>
            {
                events.Add("store");
                return Task.CompletedTask;
            },
            oldPointSets,
            (_, _) =>
            {
                events.Add("retire");
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["store", "retire", "retire"], events);
    }

    [Fact]
    public async Task StoreBeforeRetiringAsync_DoesNotDeleteOldPointsWhenStorageFails()
    {
        var retireCalls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RepositoryIndexer.StoreBeforeRetiringAsync(
                _ => throw new InvalidOperationException("embedding failed"),
                [[Guid.NewGuid()]],
                (_, _) =>
                {
                    retireCalls++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken));

        Assert.Equal(0, retireCalls);
    }
}
