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
}
