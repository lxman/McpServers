using McpGateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// LiveRegistryPath and StatePath are required on GatewayBuildOptions precisely so a test cannot
/// reach the real machine-wide location by forgetting to set them. The log path escaped that rule
/// and was hardcoded, so every test that built a gateway wrote into the LIVE gateway's log file --
/// thousands of lines of test noise in the one file a real incident has to be diagnosed from.
/// </summary>
public sealed class GatewayLogPathTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-logpath-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Build_WritesItsLog_WhereTheOptionsSay()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "demo": {
            "project": "McpGateway.TestBackend/McpGateway.TestBackend.csproj",
            "assembly": "McpGateway.TestBackend.dll",
            "deployRoot": "deploy/demo"
          }
        }
        """);

        string logDirectory = Path.Combine(_root, "logs");

        await using WebApplication app = GatewayApp.Build(new GatewayBuildOptions
        {
            ManifestPath = manifestPath,
            TokenPath = Path.Combine(_root, "token"),
            LiveRegistryPath = Path.Combine(_root, "live"),
            StatePath = TestState.Write(_root, ("demo", "v-one")),
            LogPath = Path.Combine(logDirectory, "gateway-.log"),
            RepoRoot = _root,
            Url = "http://127.0.0.1:0"
        });

        // Resolved from this app's own container, not Serilog's static Log: the sink creates its
        // file on the first write, and the whole point is that THIS gateway's records go to THIS
        // gateway's path even while other gateways are being built alongside it.
        app.Services.GetRequiredService<ILogger<GatewayLogPathTests>>()
            .LogInformation("probe from {Test}", nameof(Build_WritesItsLog_WhereTheOptionsSay));

        Assert.True(
            Directory.Exists(logDirectory) && Directory.EnumerateFiles(logDirectory).Any(),
            $"Expected a gateway log under {logDirectory}, but the sink went somewhere else.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
