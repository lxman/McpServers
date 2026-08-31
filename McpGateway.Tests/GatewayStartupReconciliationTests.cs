using System.Diagnostics;
using McpGateway.Supervision;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace McpGateway.Tests;

/// <summary>
/// The gateway must clear the previous run's orphans before it serves anything. Without it,
/// register-gateway-task.ps1's `-RestartCount 3` turns a gateway crash into two live code-assist
/// backends writing the same machine-wide index.
/// <para>
/// Uses a real `cmd /c pause` as the stand-in orphan, for the same reason as
/// <see cref="LiveBackendRegistryTests"/>: the behaviour under test is the OS interaction, and no
/// backend -- least of all CodeAssist -- is ever started here.
/// </para>
/// </summary>
public sealed class GatewayStartupReconciliationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-reconcile-" + Guid.NewGuid().ToString("N"));

    private readonly List<Process> _spawned = [];

    private string LivePath => Path.Combine(_root, "live");

    public GatewayStartupReconciliationTests()
    {
        Directory.CreateDirectory(_root);

        File.WriteAllText(Path.Combine(_root, "servers.json"), """
        {
          "demo": {
            "project": "Demo/Demo.csproj", "assembly": "Demo.dll",
            "deployRoot": "deploy/demo", "pool": "shared", "startupTimeoutSeconds": 10
          }
        }
        """);
    }

    private Process StartHarmlessProcess()
    {
        Process process = Process.Start(new ProcessStartInfo("cmd.exe", "/c pause")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;

        _spawned.Add(process);
        return process;
    }

    private WebApplication BuildGateway() => GatewayApp.Build(new GatewayBuildOptions
    {
        ManifestPath = Path.Combine(_root, "servers.json"),
        TokenPath = Path.Combine(_root, "token"),
        LiveRegistryPath = LivePath,
        StatePath = TestState.Write(_root, ("demo", "v-one")),
        RepoRoot = _root,
        Url = "http://127.0.0.1:0"
    });

    [Fact]
    public async Task Build_KillsABackendLeftBehindByAPreviousRun()
    {
        Process orphan = StartHarmlessProcess();

        new LiveBackendRegistry(LivePath, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
            .Record(new LiveBackendRecord(
                "demo", "", "v-one", orphan.Id, 51000, new DateTimeOffset(orphan.StartTime)));

        // Build, not Start: reconciliation has to happen before anything is served, and nothing
        // here should need a listening gateway to observe it.
        await using WebApplication app = BuildGateway();

        Assert.True(orphan.WaitForExit(10_000), "the orphan from the previous run was left running");
        Assert.Empty(Directory.GetFiles(LivePath, "*.json"));
    }

    [Fact]
    public async Task Build_LeavesAProcessThatMerelyInheritedTheRecordedPid()
    {
        Process innocent = StartHarmlessProcess();

        new LiveBackendRegistry(LivePath, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
            .Record(new LiveBackendRecord(
                "demo", "", "v-one", innocent.Id, 51000,
                new DateTimeOffset(innocent.StartTime).AddHours(-1)));

        await using WebApplication app = BuildGateway();

        innocent.Refresh();
        Assert.False(innocent.HasExited, "gateway startup killed a process it had not started");
    }

    public void Dispose()
    {
        foreach (Process process in _spawned)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or SystemException)
            {
                // Already gone.
            }

            process.Dispose();
        }

        try { Directory.Delete(_root, recursive: true); }
        catch (Exception ex) when (ex is IOException or DirectoryNotFoundException) { }
    }
}
