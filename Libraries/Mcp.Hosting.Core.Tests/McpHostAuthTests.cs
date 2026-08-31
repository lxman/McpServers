using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using Xunit;

namespace Mcp.Hosting.Core.Tests;

/// <summary>
/// A backend's loopback port is reachable by every process on the machine, so the token is the only
/// thing between an arbitrary local process and, say, code-assist's delete_index. A backend with no
/// token must therefore refuse to start rather than serve unauthenticated.
/// </summary>
public sealed class McpHostAuthTests
{
    private static WebApplication BuildHost(string? token)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(new McpHostOptions
        {
            ServerName = "unit-test",
            AuthToken = token
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMcpServer().WithHttpTransport();

        return builder.Build();
    }

    [Fact]
    public void MapMcpHost_Throws_WhenNoTokenIsConfigured()
    {
        WebApplication app = BuildHost(token: null);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => app.MapMcpHost());

        Assert.Contains("MCP_SHUTDOWN_TOKEN", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapMcpHost_Throws_WhenTheTokenIsEmpty()
    {
        WebApplication app = BuildHost(token: "");

        Assert.Throws<InvalidOperationException>(() => app.MapMcpHost());
    }

    [Fact]
    public void MapMcpHost_Maps_WhenATokenIsConfigured()
    {
        WebApplication app = BuildHost(token: "a-token");

        // No exception, and the same application instance comes back for chaining.
        Assert.Same(app, app.MapMcpHost());
    }
}
