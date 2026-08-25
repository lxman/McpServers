using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public class VersionedCacheTests
{
    [Fact]
    public void Invalidate_RemovesTheCachedValue()
    {
        var cache = new VersionedCache<object>();
        long version = cache.CaptureVersion("repo");
        Assert.True(cache.TryStore("repo", version, new object()));

        cache.Invalidate("repo");

        Assert.Null(cache.Get("repo"));
    }

    [Fact]
    public void TryStore_RejectsAValueBuiltBeforeInvalidation()
    {
        var cache = new VersionedCache<object>();
        long staleVersion = cache.CaptureVersion("repo");

        cache.Invalidate("repo");

        Assert.False(cache.TryStore("repo", staleVersion, new object()));
        Assert.Null(cache.Get("repo"));
    }

    [Fact]
    public void Invalidation_IsScopedToOneCollection()
    {
        var cache = new VersionedCache<object>();
        var other = new object();
        Assert.True(cache.TryStore("first", cache.CaptureVersion("first"), new object()));
        Assert.True(cache.TryStore("second", cache.CaptureVersion("second"), other));

        cache.Invalidate("first");

        Assert.Same(other, cache.Get("second"));
    }
}
