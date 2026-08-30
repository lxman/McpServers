using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using McpGateway;
using McpGateway.Supervision;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpGateway.Tests;

public sealed class AdminEndpointTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-admin-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "demo": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "per-client",
            "overlapAllowed": true, "startupTimeoutSeconds": 10
          }
        }
        """);

        _app = GatewayApp.Build(new GatewayBuildOptions
        {
            ManifestPath = manifestPath,
            TokenPath = Path.Combine(_root, "token"),
            RepoRoot = _root,
            Url = "http://127.0.0.1:0"
        }, services => services.AddSingleton<IBackendLauncher>(_launcher));

        await _app.StartAsync();

        int port = new Uri(_app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        _client.DefaultRequestHeaders.Add(
            "Authorization", $"Bearer {File.ReadAllText(Path.Combine(_root, "token")).Trim()}");
    }

    [Fact]
    public async Task Servers_ReportsLiveBackendState()
    {
        var start = new HttpRequestMessage(HttpMethod.Post, "/demo/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" })
        };
        start.Headers.Add("X-Mcp-Client", "code");
        await _client.SendAsync(start, TestContext.Current.CancellationToken);

        string body = await _client.GetStringAsync(
            "/admin/servers", TestContext.Current.CancellationToken);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement demo = doc.RootElement.GetProperty("demo");

        Assert.Equal("v-one", demo.GetProperty("activeVersion").GetString());
        Assert.Equal(1, demo.GetProperty("backends").GetArrayLength());
        Assert.Equal("code",
            demo.GetProperty("backends")[0].GetProperty("poolKey").GetString());
    }

    [Fact]
    public async Task Activate_ReturnsTheResult()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/admin/servers/demo/activate",
            new { version = "v-two" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using JsonDocument doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.Equal("v-two", doc.RootElement.GetProperty("toVersion").GetString());
    }

    [Fact]
    public async Task Activate_Returns409_WhenTheNewVersionFailsToStart()
    {
        var start = new HttpRequestMessage(HttpMethod.Post, "/demo/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" })
        };
        start.Headers.Add("X-Mcp-Client", "code");
        await _client.SendAsync(start, TestContext.Current.CancellationToken);

        _launcher.Unhealthy = true;

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/admin/servers/demo/activate",
            new { version = "v-two" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Activate_Returns404_ForAnUnknownServer()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/admin/servers/nope/activate",
            new { version = "v-two" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stop_ShutsDownEveryBackendOfAServer()
    {
        foreach (string client in new[] { "code", "desktop" })
        {
            var start = new HttpRequestMessage(HttpMethod.Post, "/demo/mcp")
            {
                Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" })
            };
            start.Headers.Add("X-Mcp-Client", client);
            await _client.SendAsync(start, TestContext.Current.CancellationToken);
        }

        HttpResponseMessage response = await _client.PostAsync(
            "/admin/servers/demo/stop", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await _client.GetStringAsync(
            "/admin/servers", TestContext.Current.CancellationToken);
        using JsonDocument doc = JsonDocument.Parse(body);

        Assert.Equal(0,
            doc.RootElement.GetProperty("demo").GetProperty("backends").GetArrayLength());
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
