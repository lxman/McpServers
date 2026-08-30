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
}
