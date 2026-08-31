using McpGateway.Configuration;
using McpGateway.Routing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace McpGateway.Tests;

public sealed class ClientIdentityTests
{
    private static ServerEntry Entry(string pool) => new()
    {
        Project = "Demo/Demo.csproj",
        Assembly = "Demo.dll",
        DeployRoot = "deploy/demo",
        ActiveVersion = "v-one",
        Pool = pool
    };

    private static HttpContext ContextWith(string? clientId)
    {
        var context = new DefaultHttpContext();
        if (clientId is not null) context.Request.Headers["X-Mcp-Client"] = clientId;
        return context;
    }

    [Fact]
    public void SharedServers_IgnoreTheClientHeader()
    {
        Assert.Equal("", ClientIdentity.ResolvePoolKey(ContextWith("code"), Entry("shared")));
        Assert.Equal("", ClientIdentity.ResolvePoolKey(ContextWith("desktop"), Entry("shared")));
    }

    [Fact]
    public void PerClientServers_KeyOnTheClientHeader()
    {
        Assert.Equal("code",
            ClientIdentity.ResolvePoolKey(ContextWith("code"), Entry("per-client")));
        Assert.Equal("desktop",
            ClientIdentity.ResolvePoolKey(ContextWith("desktop"), Entry("per-client")));
    }

    [Fact]
    public void PerClientServers_FallBackToDefault_WhenTheHeaderIsMissingOrBlank()
    {
        Assert.Equal("default",
            ClientIdentity.ResolvePoolKey(ContextWith(null), Entry("per-client")));
        Assert.Equal("default",
            ClientIdentity.ResolvePoolKey(ContextWith("   "), Entry("per-client")));
    }

    [Fact]
    public void PoolKeys_AreCaseInsensitiveAndTrimmed()
    {
        Assert.Equal("code",
            ClientIdentity.ResolvePoolKey(ContextWith(" CODE "), Entry("per-client")));
    }

    private static HttpContext ContextOn(LoopbackPair pair, string? clientId)
    {
        HttpContext context = ContextWith(clientId);
        context.Connection.RemotePort = pair.ClientPort;
        context.Connection.LocalPort = pair.ServerPort;
        return context;
    }

    [Fact]
    public void PerSessionServers_KeyOnTheProcessThatOwnsTheConnection()
    {
        using var pair = new LoopbackPair();

        string key = ClientIdentity.ResolvePoolKey(ContextOn(pair, "code"), Entry("per-session"));

        Assert.Contains(Environment.ProcessId.ToString(), key);
        Assert.NotEqual("default", key);
    }

    /// <summary>
    /// The header is what per-session replaces. Two callers sending different X-Mcp-Client values
    /// down one connection are one session and must land on one backend -- if this passes while
    /// keyed on the header, per-session is per-client wearing a different name.
    /// </summary>
    [Fact]
    public void PerSessionServers_IgnoreTheClientHeader()
    {
        using var pair = new LoopbackPair();

        Assert.Equal(
            ClientIdentity.ResolvePoolKey(ContextOn(pair, "code"), Entry("per-session")),
            ClientIdentity.ResolvePoolKey(ContextOn(pair, "desktop"), Entry("per-session")));
    }

    /// <summary>
    /// Sends a client header on purpose: falling through to the per-client branch would answer
    /// "code" here, so a green result really does mean the fallback ran.
    /// </summary>
    [Fact]
    public void PerSessionServers_FallBackToDefault_WhenTheConnectionCannotBeResolved()
    {
        HttpContext context = ContextWith("code");
        context.Connection.RemotePort = 1;
        context.Connection.LocalPort = 2;

        Assert.Equal("default", ClientIdentity.ResolvePoolKey(context, Entry("per-session")));
    }

    [Fact]
    public void SharedServers_StillIgnoreEverything_EvenOnARealConnection()
    {
        using var pair = new LoopbackPair();

        Assert.Equal("", ClientIdentity.ResolvePoolKey(ContextOn(pair, "code"), Entry("shared")));
    }

    /// <summary>
    /// HTTP clients open more than one connection and recycle them. If the key moved with the
    /// connection rather than the process, a single session would acquire a fresh backend every
    /// time its pool opened a socket -- unbounded process growth, and none of them sharing state.
    /// </summary>
    [Fact]
    public void PerSessionServers_GiveOneKeyToEveryConnectionFromTheSameProcess()
    {
        using var first = new LoopbackPair();
        using var second = new LoopbackPair();

        Assert.NotEqual(first.ClientPort, second.ClientPort);

        Assert.Equal(
            ClientIdentity.ResolvePoolKey(ContextOn(first, "code"), Entry("per-session")),
            ClientIdentity.ResolvePoolKey(ContextOn(second, "code"), Entry("per-session")));
    }
}
