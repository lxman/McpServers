using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using McpGateway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// The only test that exercises the real process launcher and the real Mcp.Hosting.Core
/// handshake. Slow by design — it publishes twice and spawns dotnet.
/// </summary>
public sealed class EndToEndSwapTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-e2e-" + Guid.NewGuid().ToString("N"));

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private string? _sessionId;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_root);

        Publish("v-one");
        Publish("v-two");

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "echo": {
            "project": "McpGateway.TestBackend/McpGateway.TestBackend.csproj",
            "assembly": "McpGateway.TestBackend.dll",
            "deployRoot": "deploy/echo",
            "activeVersion": "v-one",
            "pool": "per-client",
            "overlapAllowed": true,
            "startupTimeoutSeconds": 60
          }
        }
        """);

        _app = GatewayApp.Build(new GatewayBuildOptions
        {
            ManifestPath = manifestPath,
            TokenPath = Path.Combine(_root, "token"),
            RepoRoot = _root,
            Url = "http://127.0.0.1:0"
        });

        await _app.StartAsync();

        int port = new Uri(_app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        _client.DefaultRequestHeaders.Add(
            "Authorization", $"Bearer {File.ReadAllText(Path.Combine(_root, "token")).Trim()}");
        _client.DefaultRequestHeaders.Add("X-Mcp-Client", "tests");

        // The MCP streamable-HTTP transport requires the client to advertise both media types on
        // every POST; a real MCP client SDK does this automatically. This raw HttpClient stands in
        // for one, so it must do the same or the backend's StreamableHttpHandler returns 406.
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    private void Publish(string version)
    {
        string output = Path.Combine(_root, "deploy", "echo", version);

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
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"publish {version} failed: {stderr}");
    }

    private static string RepoPath(string relative) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", relative));

    // McpGateway.TestBackend runs HttpServerSessionMode.StatefulForInitializeClients, so a
    // "tools/call" with no session is rejected outright (-32000, "must initialize first"). A real
    // MCP client SDK performs the initialize handshake once and reuses the Mcp-Session-Id it gets
    // back for the life of the connection -- and, per the transport spec, re-initializes when a
    // session it holds turns up 404 (Session not found), which is exactly what happens here: the
    // backend a session was minted on is a separate OS process from whatever backend answers after
    // a version swap, and the new process has never heard of that session id. Caching the session
    // and only re-initializing on a 404 mirrors that reconnect behaviour, and confines the
    // extra initialize round trip to the one real swap in the test instead of doubling every call.
    private async Task<string> CallEchoAsync()
    {
        _sessionId ??= await InitializeSessionAsync();

        HttpResponseMessage response = await SendToolsCallAsync(_sessionId);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            _sessionId = await InitializeSessionAsync();
            response = await SendToolsCallAsync(_sessionId);
        }

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        return body;
    }

    private async Task<HttpResponseMessage> SendToolsCallAsync(string sessionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/echo/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new { name = "echo_version", arguments = new { } }
            })
        };
        request.Headers.Add("Mcp-Session-Id", sessionId);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> InitializeSessionAsync()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/echo/mcp", new
        {
            jsonrpc = "2.0",
            id = 0,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "mcp-e2e-tests", version = "1.0" }
            }
        }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return response.Headers.GetValues("Mcp-Session-Id").First();
    }

    [Fact]
    public async Task RealBackend_StartsLazilyAndAnswers()
    {
        string body = await CallEchoAsync();

        Assert.Contains("v-one", body);
        Assert.Contains("tests", body);
    }

    [Fact]
    public async Task Swap_UnderContinuousLoad_LosesNoCalls()
    {
        await CallEchoAsync();

        using var stop = new CancellationTokenSource();
        var failures = new List<string>();
        var sawNewVersion = false;

        Task load = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    string body = await CallEchoAsync();
                    if (body.Contains("v-two")) sawNewVersion = true;
                }
                catch (Exception ex)
                {
                    lock (failures) failures.Add(ex.Message);
                }
            }
        }, TestContext.Current.CancellationToken);

        HttpResponseMessage activate = await _client.PostAsJsonAsync(
            "/admin/servers/echo/activate",
            new { version = "v-two" },
            TestContext.Current.CancellationToken);

        activate.EnsureSuccessStatusCode();

        // Let the loop observe the new version before stopping.
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        await stop.CancelAsync();
        await load;

        Assert.True(failures.Count == 0,
            $"{failures.Count} call(s) failed during the swap: {string.Join("; ", failures.Take(3))}");
        Assert.True(sawNewVersion, "load loop never saw v-two");
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();

        try { Directory.Delete(_root, recursive: true); }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException) { }
    }
}
