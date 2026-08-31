using System.Net;
using System.Net.Http.Json;
using McpGateway;
using McpGateway.Security;
using McpGateway.Supervision;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpGateway.Tests;

public sealed class RoutingTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-routing-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string _token = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "shared-demo": {
            "project": "Demo/Demo.csproj", "assembly": "Demo.dll",
            "deployRoot": "deploy/demo", "pool": "shared", "startupTimeoutSeconds": 10
          },
          "client-demo": {
            "project": "Demo/Demo.csproj", "assembly": "Demo.dll",
            "deployRoot": "deploy/demo", "pool": "per-client", "startupTimeoutSeconds": 10
          },
          "undeployed": {
            "project": "Demo/Demo.csproj", "assembly": "Demo.dll",
            "deployRoot": "deploy/demo", "pool": "shared", "startupTimeoutSeconds": 10
          }
        }
        """);

        _app = GatewayApp.Build(new GatewayBuildOptions
        {
            ManifestPath = manifestPath,
            TokenPath = Path.Combine(_root, "token"),
            LiveRegistryPath = Path.Combine(_root, "live"),
            StatePath = TestState.Write(_root, ("shared-demo", "v-one"), ("client-demo", "v-one")),
            RepoRoot = _root,
            Url = "http://127.0.0.1:0"
        }, services => services.AddSingleton<IBackendLauncher>(_launcher));

        await _app.StartAsync();

        _token = File.ReadAllText(Path.Combine(_root, "token")).Trim();

        int port = new Uri(_app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
    }

    private async Task<HttpResponseMessage> PostMcpAsync(string server, string? clientId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/{server}/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" })
        };
        if (clientId is not null) request.Headers.Add("X-Mcp-Client", clientId);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task McpRoute_StartsTheBackendAndForwards()
    {
        HttpResponseMessage response = await PostMcpAsync("shared-demo", "code");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _launcher.StartCount);
    }

    [Fact]
    public async Task SharedServer_ServesEveryClientFromOneBackend()
    {
        await PostMcpAsync("shared-demo", "code");
        await PostMcpAsync("shared-demo", "desktop");

        Assert.Equal(1, _launcher.StartCount);
    }

    [Fact]
    public async Task PerClientServer_GivesEachClientItsOwnBackend()
    {
        await PostMcpAsync("client-demo", "code");
        await PostMcpAsync("client-demo", "desktop");
        await PostMcpAsync("client-demo", "code");

        Assert.Equal(2, _launcher.StartCount);
    }

    [Fact]
    public async Task Forwarding_KeepsTheClientHeader_SwapsInTheBackendToken_AndPreservesTheQuery()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/shared-demo/mcp?trace=abc123")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" })
        };
        request.Headers.Add("X-Mcp-Client", "code");

        HttpResponseMessage response = await _client.SendAsync(
            request, TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        string backendToken = _app.Services.GetRequiredService<BackendToken>().Value;

        Assert.Contains("\"clientHeader\":\"code\"", body);
        Assert.Contains($"\"authHeader\":\"Bearer {backendToken}\"", body);

        // The caller's own token must not reach the backend: it is the client-facing credential,
        // and a backend has no business being able to replay it against the gateway.
        Assert.DoesNotContain(_token, body, StringComparison.Ordinal);
        Assert.Contains("trace=abc123", body);
    }

    [Fact]
    public async Task UnknownServer_Is404()
    {
        HttpResponseMessage response = await PostMcpAsync("nope", "code");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MissingToken_Is401()
    {
        using var bare = new HttpClient { BaseAddress = _client.BaseAddress };

        HttpResponseMessage response = await bare.PostAsync(
            "/shared-demo/mcp", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Configured but never deployed: no version was ever recorded for it. That has to fail as a
    /// 503 that says so, not resolve to a deploy directory named after a placeholder and fail much
    /// later as a missing file.
    /// </summary>
    [Fact]
    public async Task ServerWithNoRecordedVersion_Is503SayingItWasNeverDeployed()
    {
        HttpResponseMessage response = await PostMcpAsync("undeployed", "code");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Contains("no active version recorded", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _launcher.StartCount);
    }

    [Fact]
    public async Task FailedStart_Is503WithDetail()
    {
        _launcher.SuppressPortFile = true;

        HttpResponseMessage response = await PostMcpAsync("shared-demo", "code");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("port file", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthRoute_ProxiesTheBackend()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/shared-demo/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("v-one", body);
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
