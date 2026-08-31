using System.Diagnostics;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
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

        new LiveBackendRegistry(LivePath, NullLogger.Instance)
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

        new LiveBackendRegistry(LivePath, NullLogger.Instance)
            .Record(new LiveBackendRecord(
                "demo", "", "v-one", innocent.Id, 51000,
                new DateTimeOffset(innocent.StartTime).AddHours(-1)));

        await using WebApplication app = BuildGateway();

        innocent.Refresh();
        Assert.False(innocent.HasExited, "gateway startup killed a process it had not started");
    }

    /// <summary>
    /// Reconciliation cannot tell an orphan from a live backend of a gateway that is still running:
    /// both are records naming a live process whose start time matches. So a second gateway must be
    /// refused before it reads the registry at all -- otherwise starting one by hand, or Task
    /// Scheduler restarting one while the first is still exiting, destroys a live backend and then
    /// dies on the port anyway.
    /// </summary>
    [Fact]
    public async Task Build_Refuses_WhenAnotherGatewayIsRunning()
    {
        await using WebApplication running = BuildGateway();

        InvalidOperationException refusal =
            Assert.Throws<InvalidOperationException>(() => BuildGateway());

        Assert.Contains("already running", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Separate from the refusal test on purpose: if both lived together, an Assert.Throws that
    /// fails first would mask the assertion that actually matters -- whether the live backend
    /// survived. Here the refusal is swallowed so the survival check always runs.
    /// </summary>
    [Fact]
    public async Task Build_ReconcilesNothing_WhenItIsRefused()
    {
        await using WebApplication running = BuildGateway();

        // Recorded only after the first gateway has taken the guard, so it stands in for a backend
        // the *running* gateway owns rather than an orphan. Reconciliation cannot tell them apart.
        Process live = StartHarmlessProcess();

        new LiveBackendRegistry(LivePath, NullLogger.Instance)
            .Record(new LiveBackendRecord(
                "demo", "", "v-one", live.Id, 51000, new DateTimeOffset(live.StartTime)));

        try
        {
            await using WebApplication second = BuildGateway();
        }
        catch (InvalidOperationException)
        {
            // Expected; asserted on by the test above.
        }

        live.Refresh();

        Assert.False(live.HasExited,
            "the second gateway reconciled the running gateway's live backend and killed it");

        Assert.Single(Directory.GetFiles(LivePath, "*.json"));
    }

    /// <summary>Once the first gateway is gone, the name is free again.</summary>
    [Fact]
    public async Task Build_Succeeds_AfterTheRunningGatewayIsDisposed()
    {
        await using (WebApplication first = BuildGateway()) { }

        await using WebApplication second = BuildGateway();
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
