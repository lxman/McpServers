using System.Diagnostics;
using McpGateway.Routing;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// These run against a real loopback socket rather than a fake. The whole point of
/// <see cref="SessionIdentity"/> is that it reads the OS TCP table correctly, and a P/Invoke with a
/// subtly wrong struct layout or an unswapped port still compiles and still returns something.
/// </summary>
public sealed class SessionIdentityTests
{
    [Fact]
    public void TryResolvePid_OfALoopbackConnection_FindsTheProcessThatOpenedIt()
    {
        using var pair = new LoopbackPair();

        int? pid = SessionIdentity.TryResolvePid(pair.ClientPort, pair.ServerPort);

        Assert.Equal(Environment.ProcessId, pid);
    }

    /// <summary>
    /// Matching on the local port alone would pass the test above and still be wrong: every
    /// connection a client makes shares that process, but the gateway must not treat a connection to
    /// some other listener as one of its own. Same live client port, wrong far end.
    /// </summary>
    [Fact]
    public void TryResolvePid_ReturnsNull_WhenTheRemotePortDoesNotMatch()
    {
        using var pair = new LoopbackPair();

        int wrongRemotePort = pair.ServerPort == 65535 ? 65534 : pair.ServerPort + 1;

        Assert.Null(SessionIdentity.TryResolvePid(pair.ClientPort, wrongRemotePort));
    }

    /// <summary>
    /// Windows reuses pids. Without the start time a new session that happens to land on a dead
    /// session's pid would silently inherit its backend -- the exact cross-session bleed this whole
    /// mechanism exists to prevent.
    /// </summary>
    [Fact]
    public void FormatKey_SeparatesTwoProcessesThatShareAPid()
    {
        var first = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var second = DateTimeOffset.FromUnixTimeSeconds(1_700_000_001);

        Assert.NotEqual(
            SessionIdentity.FormatKey(4242, first),
            SessionIdentity.FormatKey(4242, second));
    }

    [Fact]
    public void FormatKey_IsStableForTheSameProcess()
    {
        var startedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        Assert.Equal(
            SessionIdentity.FormatKey(4242, startedAt),
            SessionIdentity.FormatKey(4242, startedAt));
    }

    [Fact]
    public void TryParseKey_RoundTripsFormatKey()
    {
        var startedAt = DateTimeOffset.FromUnixTimeMilliseconds(1_788_000_000_123);
        string key = SessionIdentity.FormatKey(4242, startedAt);

        Assert.True(SessionIdentity.TryParseKey(key, out int pid, out DateTimeOffset parsed));
        Assert.Equal(4242, pid);
        Assert.Equal(startedAt, parsed);
    }

    [Fact]
    public void IsOwnerAlive_IsTrue_ForThisProcess()
    {
        using Process self = Process.GetCurrentProcess();
        string key = SessionIdentity.FormatKey(Environment.ProcessId, self.StartTime);

        Assert.True(SessionIdentity.IsOwnerAlive(key));
    }

    [Fact]
    public async Task IsOwnerAlive_IsFalse_OnceTheOwnerHasGone()
    {
        using Process child = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe"),
            "-s --max-time 120 http://127.0.0.1:1/never")
        { UseShellExecute = false, CreateNoWindow = true })!;

        string key = SessionIdentity.FormatKey(child.Id, child.StartTime);
        Assert.True(SessionIdentity.IsOwnerAlive(key));

        child.Kill(entireProcessTree: true);
        await child.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.False(SessionIdentity.IsOwnerAlive(key));
    }

    /// <summary>
    /// Windows reuses pids. A key naming a live pid that started at a different time belongs to a
    /// session that is gone, and treating it as alive would keep a dead session's backend forever.
    /// </summary>
    [Fact]
    public void IsOwnerAlive_IsFalse_WhenThePidBelongsToADifferentProcessNow()
    {
        using Process self = Process.GetCurrentProcess();
        string key = SessionIdentity.FormatKey(
            Environment.ProcessId, self.StartTime.AddSeconds(-30));

        Assert.False(SessionIdentity.IsOwnerAlive(key));
    }

    /// <summary>
    /// "default" is what ResolvePoolKey falls back to when the owner could not be established.
    /// There is no owner to check, so it must never be treated as a dead one -- reaping on a key
    /// we cannot reason about would stop live backends.
    /// </summary>
    [Fact]
    public void IsOwnerAlive_IsTrue_ForAKeyItCannotParse()
    {
        Assert.True(SessionIdentity.IsOwnerAlive("default"));
        Assert.True(SessionIdentity.IsOwnerAlive(""));
        Assert.True(SessionIdentity.IsOwnerAlive("s-notanumber-123"));
    }
}
