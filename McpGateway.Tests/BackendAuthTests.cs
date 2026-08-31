using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using McpGateway.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// Publishes and spawns a real Mcp.Hosting.Core backend behind a real gateway, then talks to the
/// backend's loopback port directly -- which is exactly what any other process on this machine can
/// do. Shared across the class because a publish plus a process spawn is too slow to repeat per
/// test.
/// </summary>
public sealed class BackendAuthFixture : IAsyncLifetime
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(), "mcp-backendauth-" + Guid.NewGuid().ToString("N"));

    public WebApplication? App { get; private set; }

    /// <summary>The client-facing bearer token, the one written to disk and given to clients.</summary>
    public string ClientToken { get; private set; } = null!;

    /// <summary>The gateway-to-backend token, minted in memory and never handed to a client.</summary>
    public string BackendToken { get; private set; } = null!;

    /// <summary>The loopback port the spawned backend is listening on.</summary>
    public int BackendPort { get; private set; }

    public string TokenFilePath => Path.Combine(Root, "token");

    /// <summary>A client of the gateway, carrying the client token like Claude Code does.</summary>
    public HttpClient Gateway { get; private set; } = null!;

    /// <summary>A client of the backend's own port, carrying nothing until a test adds it.</summary>
    public HttpClient Backend { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Root);
        Publish("v-one");

        string manifestPath = Path.Combine(Root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "echo": {
            "project": "McpGateway.TestBackend/McpGateway.TestBackend.csproj",
            "assembly": "McpGateway.TestBackend.dll",
            "deployRoot": "deploy/echo",
            "activeVersion": "v-one",
            "pool": "shared",
            "startupTimeoutSeconds": 60
          }
        }
        """);

        App = GatewayApp.Build(new GatewayBuildOptions
        {
            ManifestPath = manifestPath,
            TokenPath = TokenFilePath,
            LiveRegistryPath = Path.Combine(Root, "live"),
            RepoRoot = Root,
            Url = "http://127.0.0.1:0"
        });

        await App.StartAsync();

        int gatewayPort = new Uri(App.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

        ClientToken = File.ReadAllText(TokenFilePath).Trim();
        BackendToken = App.Services.GetRequiredService<BackendToken>().Value;

        Gateway = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{gatewayPort}") };
        Gateway.DefaultRequestHeaders.Add("Authorization", $"Bearer {ClientToken}");
        Gateway.DefaultRequestHeaders.Add("X-Mcp-Client", "tests");
        AddMcpAcceptHeaders(Gateway);

        // Drives the lazy start, so the backend is up and a port is discoverable below.
        HttpResponseMessage handshake = await Gateway.PostAsJsonAsync("/echo/mcp", InitializeBody);
        handshake.EnsureSuccessStatusCode();

        BackendPort = await ReadBackendPortAsync();

        Backend = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{BackendPort}") };
        AddMcpAcceptHeaders(Backend);
    }

    public static object InitializeBody => new
    {
        jsonrpc = "2.0",
        id = 0,
        method = "initialize",
        @params = new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "mcp-backend-auth-tests", version = "1.0" }
        }
    };

    /// <summary>
    /// The MCP streamable-HTTP transport rejects a POST that does not advertise both media types.
    /// A real client SDK does this automatically; these raw HttpClients must do it by hand.
    /// </summary>
    private static void AddMcpAcceptHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    private async Task<int> ReadBackendPortAsync()
    {
        using JsonDocument servers = JsonDocument.Parse(
            await Gateway.GetStringAsync("/admin/servers"));

        return servers.RootElement
            .GetProperty("echo").GetProperty("backends")[0].GetProperty("port").GetInt32();
    }

    private void Publish(string version)
    {
        string output = Path.Combine(Root, "deploy", "echo", version);

        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string arg in new[]
                 {
                     "publish", RepoPath("McpGateway.TestBackend/McpGateway.TestBackend.csproj"),
                     "-c", "Debug", "-o", output, "--nologo", "-v", "quiet"
                 })
        {
            info.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(info)!;

        // Both pipes have to be drained concurrently. Reading stderr to the end while stdout fills
        // its 4 KB buffer deadlocks the publish -- the child blocks on a write nobody is reading
        // and WaitForExit never returns. Seen for real on a publish whose dependency graph had
        // just been rebuilt, so this is not theoretical.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0,
            $"publish {version} failed: {stderr.Result}{Environment.NewLine}{stdout.Result}");
    }

    private static string RepoPath(string relative) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", relative));

    /// <summary>
    /// Null-tolerant on purpose. If InitializeAsync throws part way through, xUnit still calls
    /// this -- and an NRE on a field that was never assigned would skip App.DisposeAsync and leave
    /// the spawned backend running as a genuine orphan, holding this run's stdout handle open.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Gateway?.Dispose();
        Backend?.Dispose();

        if (App is not null)
        {
            await App.StopAsync();
            await App.DisposeAsync();
        }

        try { Directory.Delete(Root, recursive: true); }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException) { }
    }
}

public sealed class BackendAuthTests(BackendAuthFixture fixture) : IClassFixture<BackendAuthFixture>
{
    private HttpRequestMessage BackendRequest(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Add("Authorization", $"Bearer {token}");
        return request;
    }

    [Fact]
    public async Task Backend_RejectsHealth_WithNoToken()
    {
        HttpResponseMessage response = await fixture.Backend.SendAsync(
            BackendRequest(HttpMethod.Get, "/health", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Backend_RejectsMcp_WithNoToken()
    {
        HttpRequestMessage request = BackendRequest(HttpMethod.Post, "/mcp", null);
        request.Content = JsonContent.Create(BackendAuthFixture.InitializeBody);

        HttpResponseMessage response = await fixture.Backend.SendAsync(
            request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Backend_RejectsShutdown_WithNoToken()
    {
        HttpResponseMessage response = await fixture.Backend.SendAsync(
            BackendRequest(HttpMethod.Post, "/admin/shutdown", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The whole point of minting a second token: a client that holds the gateway's bearer token
    /// still cannot drive a backend port directly, so it cannot bypass the gateway's pooling,
    /// hold and non-overlap guarantees.
    /// </summary>
    [Fact]
    public async Task Backend_RejectsHealth_WithTheClientToken()
    {
        HttpResponseMessage response = await fixture.Backend.SendAsync(
            BackendRequest(HttpMethod.Get, "/health", fixture.ClientToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Backend_RejectsMcp_WithTheClientToken()
    {
        HttpRequestMessage request = BackendRequest(HttpMethod.Post, "/mcp", fixture.ClientToken);
        request.Content = JsonContent.Create(BackendAuthFixture.InitializeBody);

        HttpResponseMessage response = await fixture.Backend.SendAsync(
            request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Backend_AcceptsHealth_WithTheBackendToken()
    {
        HttpResponseMessage response = await fixture.Backend.SendAsync(
            BackendRequest(HttpMethod.Get, "/health", fixture.BackendToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Backend_AcceptsMcp_WithTheBackendToken()
    {
        HttpRequestMessage request = BackendRequest(HttpMethod.Post, "/mcp", fixture.BackendToken);
        request.Content = JsonContent.Create(BackendAuthFixture.InitializeBody);

        HttpResponseMessage response = await fixture.Backend.SendAsync(
            request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void BackendToken_IsNotTheClientToken()
    {
        Assert.NotEqual(fixture.ClientToken, fixture.BackendToken);
    }

    /// <summary>
    /// In memory only. If it ever reached the token file, every client would be handed it along
    /// with the one it is supposed to have.
    /// </summary>
    [Fact]
    public void BackendToken_IsNeverWrittenToTheTokenFile()
    {
        string onDisk = File.ReadAllText(fixture.TokenFilePath);

        Assert.DoesNotContain(fixture.BackendToken, onDisk, StringComparison.Ordinal);
    }

    /// <summary>
    /// The end-to-end path still works: the health gate reached a now-guarded /health, and the
    /// forwarder presented the backend token on /mcp rather than stripping Authorization.
    /// </summary>
    [Fact]
    public async Task Gateway_StillForwardsSuccessfully_EndToEnd()
    {
        HttpResponseMessage initialize = await fixture.Gateway.PostAsJsonAsync(
            "/echo/mcp", BackendAuthFixture.InitializeBody, TestContext.Current.CancellationToken);

        initialize.EnsureSuccessStatusCode();
        string sessionId = initialize.Headers.GetValues("Mcp-Session-Id").First();

        var call = new HttpRequestMessage(HttpMethod.Post, "/echo/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new { name = "echo_version", arguments = new { } }
            })
        };
        call.Headers.Add("Mcp-Session-Id", sessionId);

        HttpResponseMessage response = await fixture.Gateway.SendAsync(
            call, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Contains("v-one", body);
    }
}
