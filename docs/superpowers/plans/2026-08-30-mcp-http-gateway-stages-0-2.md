# MCP HTTP Gateway (Stages 0–2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a gateway that supervises MCP servers as HTTP backends, then move CodeAssist behind it as a single shared instance that can be upgraded without a running session seeing a failed tool call.

**Architecture:** One `McpGateway` process owns `127.0.0.1:7300` and lazily spawns backends on ephemeral ports, keyed by `(server, poolKey)` where `poolKey` comes from an `X-Mcp-Client` header. A shared `Mcp.Hosting.Core` library gives every server the same HTTP host shape — Kestrel on port 0, a port file, `/health`, `/mcp`, absolute log paths. Upgrades are a blue/green route flip with drain; servers that can't run two instances get a hold-and-swap path instead.

**Tech Stack:** .NET 10, ASP.NET Core, `ModelContextProtocol` 2.2.0 + `ModelContextProtocol.AspNetCore` 2.2.0, YARP 2.3.0 (`IHttpForwarder`), Serilog, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-08-30-mcp-http-gateway-design.md`

## Global Constraints

- Target framework is `net10.0` for every new project. Central package management is on — add versions to `Directory.Packages.props`, and reference without a version in `.csproj`.
- Gateway listens on `http://127.0.0.1:7300`. Bind loopback only, never `0.0.0.0`.
- Backends bind `http://127.0.0.1:0`. The OS picks the port; the backend reports it via its port file.
- Bearer token lives at `%LOCALAPPDATA%\McpGateway\token`, 32 random bytes base64url-encoded, generated on first run. Compared with `CryptographicOperations.FixedTimeEquals`.
- Client id header is `X-Mcp-Client`. Absent means `default`.
- Backend logs go to `%LOCALAPPDATA%\McpServers\logs\<name>\<name>-.log`, rolling daily. Never a CWD-relative path.
- Version id format is `v-<short-sha>-<utc-timestamp>`, e.g. `v-146874c-20260830T1214`.
- Deploy root is `deploy/<server>/<version>/`. Nothing runs out of `bin/` after this work.
- `servers.json` at the repo root is the manifest and the source of truth for `activeVersion`. No directory junctions.
- Session mode is `HttpServerSessionMode.StatefulForInitializeClients` on every converted server.
- The gateway takes no project reference on any MCP server.
- Test framework is xUnit v3. Use `TestContext.Current.CancellationToken` for cancellation tokens in tests, matching `Libraries/CodeAssist.Core.Tests`.
- Every test in this plan must be verified by breaking the thing it covers and confirming it goes red. A test that passes with the implementation deleted is a plan failure, not a pass.

---

## File Structure

**New — `Libraries/Mcp.Hosting.Core/`** (shared host shape for every converted server)

| File | Responsibility |
|---|---|
| `Mcp.Hosting.Core.csproj` | project file |
| `McpHttpHost.cs` | `CreateBuilder(args, serverName)` — Kestrel on port 0, Serilog to absolute path, options binding |
| `McpHostOptions.cs` | `ServerName`, `PortFilePath`, `ShutdownToken`, `Version` |
| `McpHostApplicationExtensions.cs` | `MapMcpHost()` — maps `/mcp`, `/health`, `/admin/shutdown`, writes port file |
| `PortFile.cs` | atomic write/read of `{port,pid,startedAt}` |
| `McpCaller.cs` | `ClientId` accessor over `IHttpContextAccessor` |

**New — `McpGateway/`**

| File | Responsibility |
|---|---|
| `Program.cs` | five lines: `GatewayApp.Build(args).RunAsync()` |
| `GatewayApp.cs` | composition root, so tests build the app without `WebApplicationFactory` |
| `Configuration/ServerEntry.cs` | one manifest entry (record) |
| `Configuration/ManifestStore.cs` | load, persist, mutate `activeVersion` |
| `Security/TokenStore.cs` | generate/read the bearer token |
| `Security/BearerAuthMiddleware.cs` | reject unauthenticated requests |
| `Routing/ClientIdentity.cs` | header → pool key |
| `Routing/McpForwarder.cs` | YARP `IHttpForwarder` + path rewrite |
| `Supervision/BackendKey.cs` | `(server, poolKey)` readonly record struct |
| `Supervision/IBackendLauncher.cs` | launch abstraction so tests don't spawn processes |
| `Supervision/ProcessBackendLauncher.cs` | real `Process.Start` implementation |
| `Supervision/BackendInstance.cs` | one running backend: port, pid, in-flight counter, last-used |
| `Supervision/BackendSupervisor.cs` | pool, lazy start, health gate, idle stop, crash recovery |
| `Supervision/IdleReaper.cs` | stops backends nobody has used lately |
| `Supervision/EagerStarter.cs` | starts the servers marked `eagerStart` at gateway startup |
| `Supervision/HealthProbe.cs` | poll `/health` until ready or timeout |
| `Upgrade/ActivationService.cs` | blue/green and hold-and-swap paths |
| `Endpoints/AdminEndpoints.cs` | `/admin/*` |

**New — `McpGateway.Tests/`**, **New — `McpGateway.TestBackend/`** (a trivial real backend for integration tests)

**Modified**

| File | Change |
|---|---|
| `Directory.Packages.props` | add YARP, `ModelContextProtocol.AspNetCore` |
| `McpServers.slnx` | add the five new projects |
| `Libraries/CodeAssist.Core/Services/IndexStateStore.cs:220` | `Delete` takes `_writeLock` |
| `CodeAssistMcp/McpTools/RepositoryTools.cs:29` | `clearOtherCaches` defaults to `false` |
| `CodeAssistMcp/Program.cs` | stdio host → `McpHttpHost` |
| `CodeAssistMcp/CodeAssistMcp.csproj` | web SDK, reference `Mcp.Hosting.Core` |

---

## Stage 0 — Spike

### Task 1: Client identity probe

Throwaway. The output is an answer written into the spec, not code that survives.

Three questions: does Claude Code send anything that distinguishes a *session* rather than a client; what protocol revision does Claude Desktop negotiate; what request timeout does the client apply.

**Files:**
- Create: `spike/IdentityProbe/IdentityProbe.csproj` (deleted in Step 6)
- Create: `spike/IdentityProbe/Program.cs` (deleted in Step 6)
- Modify: `docs/superpowers/specs/2026-08-30-mcp-http-gateway-design.md` (record findings)

**Interfaces:**
- Consumes: nothing
- Produces: a decision recorded in the spec. If Claude Code sends a per-session discriminator, Task 5's `ClientIdentity.ResolvePoolKey` uses it instead of `X-Mcp-Client`, and the "per-client is not per-session" risk is struck from the spec.

- [ ] **Step 1: Write the probe**

Create `spike/IdentityProbe/IdentityProbe.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

Create `spike/IdentityProbe/Program.cs`. It answers `initialize` and `tools/list` well enough for a client to connect, and logs every request's headers and body to a file:

```csharp
using System.Text;
using System.Text.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:7399");
WebApplication app = builder.Build();

string logPath = Path.Combine(Path.GetTempPath(), "identity-probe.log");
var gate = new SemaphoreSlim(1, 1);

app.MapPost("/mcp", async (HttpContext ctx) =>
{
    ctx.Request.EnableBuffering();
    using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
    string body = await reader.ReadToEndAsync();
    ctx.Request.Body.Position = 0;

    var sb = new StringBuilder();
    sb.AppendLine($"--- {DateTimeOffset.UtcNow:O} conn={ctx.Connection.Id} ---");
    foreach ((string key, var value) in ctx.Request.Headers)
    {
        sb.AppendLine($"  {key}: {value}");
    }
    sb.AppendLine($"  BODY: {body}");

    await gate.WaitAsync();
    try { await File.AppendAllTextAsync(logPath, sb.ToString()); }
    finally { gate.Release(); }

    using JsonDocument doc = JsonDocument.Parse(body);
    JsonElement root = doc.RootElement;
    string method = root.TryGetProperty("method", out JsonElement m) ? m.GetString() ?? "" : "";
    JsonElement id = root.TryGetProperty("id", out JsonElement i) ? i : default;

    if (method == "notifications/initialized") return Results.StatusCode(202);

    object result = method switch
    {
        "initialize" => new
        {
            protocolVersion = "2025-11-25",
            capabilities = new { tools = new { } },
            serverInfo = new { name = "identity-probe", version = "0.0.1" }
        },
        "tools/list" => new
        {
            tools = new[]
            {
                new
                {
                    name = "probe_ping",
                    description = "Returns pong. Probe only.",
                    inputSchema = new { type = "object", properties = new { } }
                }
            }
        },
        "tools/call" => new
        {
            content = new[] { new { type = "text", text = "pong" } }
        },
        _ => new { }
    };

    return Results.Json(new
    {
        jsonrpc = "2.0",
        id = id.ValueKind == JsonValueKind.Undefined ? (object?)null : JsonSerializer.Deserialize<object>(id.GetRawText()),
        result
    });
});

app.Run();
```

- [ ] **Step 2: Run it and confirm it answers**

```powershell
dotnet run --project spike/IdentityProbe/IdentityProbe.csproj
```

In a second shell:

```powershell
$body = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:7399/mcp -Body $body -ContentType application/json
```

Expected: a JSON-RPC result with `serverInfo.name` of `identity-probe`. Confirm `$env:TEMP\identity-probe.log` now exists and contains the request headers.

- [ ] **Step 3: Register it with both clients**

```powershell
claude mcp add --transport http --scope user identity-probe http://127.0.0.1:7399/mcp
```

For Claude Desktop, add to `C:\Users\jorda\AppData\Roaming\Claude\claude_desktop_config.json` under `mcpServers`:

```json
"identity-probe": { "type": "http", "url": "http://127.0.0.1:7399/mcp" }
```

- [ ] **Step 4: STOP — hand back to the user**

This step cannot be automated. The probe only sees traffic after the clients reconnect.

Ask the user to: start a **new** Claude Code session, call `probe_ping` in it, start a **second** Claude Code session and call `probe_ping` there too, then open Claude Desktop and call it once. Then report back.

Do not proceed until the user confirms all three calls were made.

- [ ] **Step 5: Read the log and answer the three questions**

```powershell
Get-Content $env:TEMP\identity-probe.log
```

Answer, with evidence quoted from the log:

1. **Session discriminator?** Compare the two Claude Code sessions' request blocks. Any header differing between them that is stable *within* a session is a session discriminator. Check `Mcp-Session-Id`, `ctx.Connection.Id`, and any `X-`/`User-Agent` variation. Note that `conn=` is ASP.NET Core's connection id — it is evidence only if it is stable across a session's calls and differs between sessions.
2. **Desktop's revision?** Read `protocolVersion` from Claude Desktop's `initialize` body, and whether it sent one at all — a 2026-07-28 client sends per-request `_meta` instead of an initialize handshake.
3. **Client timeout?** Not visible in the log. Measure it: add `await Task.Delay(TimeSpan.FromSeconds(90));` at the top of the `/mcp` handler, rebuild, call `probe_ping`, and time how long the client waits before erroring. Remove the delay afterwards.

- [ ] **Step 6: Record findings and delete the spike**

Add a `## Stage 0 findings` section to the spec recording all three answers verbatim.

If question 1 answered yes, also edit the spec's **Client identity** section to use the discovered discriminator, and delete the "Per-client is not per-session" paragraph from **Risks**.

Then unregister and delete:

```powershell
claude mcp remove --scope user identity-probe
```

Remove the `identity-probe` entry from `claude_desktop_config.json`.

```bash
rm -rf spike/
git add docs/superpowers/specs/2026-08-30-mcp-http-gateway-design.md
git commit -m "docs: record Stage 0 identity probe findings"
```

Confirm `spike/` is gone and `git status --short` shows no stray files.

---

## Stage 1 — Infrastructure

### Task 2: `Mcp.Hosting.Core`

The host shape every converted server uses. Nothing MCP-server-specific lives here.

**Files:**
- Create: `Libraries/Mcp.Hosting.Core/Mcp.Hosting.Core.csproj`
- Create: `Libraries/Mcp.Hosting.Core/McpHostOptions.cs`
- Create: `Libraries/Mcp.Hosting.Core/PortFile.cs`
- Create: `Libraries/Mcp.Hosting.Core/McpCaller.cs`
- Create: `Libraries/Mcp.Hosting.Core/McpHttpHost.cs`
- Create: `Libraries/Mcp.Hosting.Core/McpHostApplicationExtensions.cs`
- Create: `Libraries/Mcp.Hosting.Core.Tests/Mcp.Hosting.Core.Tests.csproj`
- Create: `Libraries/Mcp.Hosting.Core.Tests/PortFileTests.cs`
- Modify: `Directory.Packages.props`
- Modify: `McpServers.slnx`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `McpHttpHost.CreateBuilder(string[] args, string serverName) -> WebApplicationBuilder`
  - `McpHttpHost.LogPathFor(string serverName) -> string`
  - `McpHostApplicationExtensions.MapMcpHost(this WebApplication app) -> WebApplication`
  - `PortFile.WriteAsync(string path, int port, int pid, CancellationToken) -> Task`
  - `PortFile.TryRead(string path, out PortFileContent content) -> bool`
  - `PortFileContent` — `record(int Port, int Pid, DateTimeOffset StartedAt)`
  - `McpCaller.ClientId -> string`, `McpCaller.HeaderName = "X-Mcp-Client"`, `McpCaller.Unknown = "default"`
  - CLI contract Task 4 depends on: `--mcp-port-file <path>`; env `MCP_SERVER_NAME`, `MCP_SERVER_VERSION`, `MCP_SHUTDOWN_TOKEN`

- [ ] **Step 1: Add packages and projects**

In `Directory.Packages.props`, add beside the other ASP.NET entries (near line 156):

```xml
    <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />
    <PackageVersion Include="Yarp.ReverseProxy" Version="2.3.0" />
```

Create `Libraries/Mcp.Hosting.Core/Mcp.Hosting.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="ModelContextProtocol" />
        <PackageReference Include="ModelContextProtocol.AspNetCore" />
        <PackageReference Include="Serilog" />
        <PackageReference Include="Serilog.Extensions.Logging" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\SerilogFileWriter\SerilogFileWriter.csproj" />
    </ItemGroup>

</Project>
```

Create `Libraries/Mcp.Hosting.Core.Tests/Mcp.Hosting.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" />
        <PackageReference Include="xunit.runner.visualstudio">
          <PrivateAssets>all</PrivateAssets>
          <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Mcp.Hosting.Core\Mcp.Hosting.Core.csproj" />
    </ItemGroup>

</Project>
```

In `McpServers.slnx`, add inside the `/Libraries/` folder element:

```xml
    <Project Path="Libraries\Mcp.Hosting.Core\Mcp.Hosting.Core.csproj" />
    <Project Path="Libraries\Mcp.Hosting.Core.Tests\Mcp.Hosting.Core.Tests.csproj" />
```

- [ ] **Step 2: Write the failing port-file test**

The gateway polls this file while the backend writes it, so a partial read must be impossible. Create `Libraries/Mcp.Hosting.Core.Tests/PortFileTests.cs`:

```csharp
using Xunit;

namespace Mcp.Hosting.Core.Tests;

public sealed class PortFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-portfile-" + Guid.NewGuid().ToString("N"));

    private string TargetPath => Path.Combine(_directory, "port.json");

    public PortFileTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task WriteThenRead_RoundTrips()
    {
        await PortFile.WriteAsync(TargetPath, 51234, 4242, TestContext.Current.CancellationToken);

        Assert.True(PortFile.TryRead(TargetPath, out PortFileContent content));
        Assert.Equal(51234, content.Port);
        Assert.Equal(4242, content.Pid);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenMissing()
    {
        Assert.False(PortFile.TryRead(TargetPath, out _));
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenPartial()
    {
        File.WriteAllText(TargetPath, "{\"Port\":512");

        Assert.False(PortFile.TryRead(TargetPath, out _));
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenPortIsZero()
    {
        File.WriteAllText(TargetPath, "{\"Port\":0,\"Pid\":1,\"StartedAt\":\"2026-08-30T00:00:00+00:00\"}");

        Assert.False(PortFile.TryRead(TargetPath, out _));
    }

    [Fact]
    public async Task WriteAsync_LeavesNoTempFileBehind()
    {
        await PortFile.WriteAsync(TargetPath, 51234, 4242, TestContext.Current.CancellationToken);

        string[] files = Directory.GetFiles(_directory);
        Assert.Equal(new[] { TargetPath }, files);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

- [ ] **Step 3: Run to verify it fails**

```powershell
dotnet build Libraries\Mcp.Hosting.Core.Tests\Mcp.Hosting.Core.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `PortFile` and `PortFileContent` do not exist.

- [ ] **Step 4: Implement `PortFile`**

Create `Libraries/Mcp.Hosting.Core/PortFile.cs`:

```csharp
using System.Text.Json;

namespace Mcp.Hosting.Core;

/// <summary>How a backend tells the gateway which port the OS gave it.</summary>
public sealed record PortFileContent(int Port, int Pid, DateTimeOffset StartedAt);

public static class PortFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Write via a temp file and a move. The gateway polls this path, so it must never observe a
    /// half-written file.
    /// </summary>
    public static async Task WriteAsync(
        string path, int port, int pid, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var content = new PortFileContent(port, pid, DateTimeOffset.UtcNow);
        string temp = path + ".tmp";

        await File.WriteAllTextAsync(
            temp, JsonSerializer.Serialize(content, Options), cancellationToken);

        File.Move(temp, path, overwrite: true);
    }

    public static bool TryRead(string path, out PortFileContent content)
    {
        content = null!;

        try
        {
            if (!File.Exists(path)) return false;

            PortFileContent? parsed = JsonSerializer.Deserialize<PortFileContent>(
                File.ReadAllText(path), Options);

            if (parsed is null || parsed.Port <= 0) return false;

            content = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet build Libraries\Mcp.Hosting.Core.Tests\Mcp.Hosting.Core.Tests.csproj -c Debug -m:1 -v quiet
dotnet Libraries\Mcp.Hosting.Core.Tests\bin\Debug\net10.0\Mcp.Hosting.Core.Tests.dll -noColor
```

Expected: 5 passed.

- [ ] **Step 6: Verify the tests can fail**

Change `File.Move(temp, path, overwrite: true);` to `File.Copy(temp, path, overwrite: true);`, rebuild and rerun. Expected: `WriteAsync_LeavesNoTempFileBehind` goes red. Restore the line.

Then delete the `if (parsed is null || parsed.Port <= 0) return false;` guard and rerun. Expected: `TryRead_ReturnsFalse_WhenPortIsZero` goes red. Restore it.

- [ ] **Step 7: Implement the options and the caller accessor**

Create `Libraries/Mcp.Hosting.Core/McpHostOptions.cs`:

```csharp
namespace Mcp.Hosting.Core;

public sealed class McpHostOptions
{
    public required string ServerName { get; init; }
    public string? PortFilePath { get; init; }
    public string? ShutdownToken { get; init; }
    public string Version { get; init; } = "unknown";
}
```

Create `Libraries/Mcp.Hosting.Core/McpCaller.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace Mcp.Hosting.Core;

/// <summary>
/// The calling client's identity, as supplied by the gateway. Stage 3 servers ignore this — the
/// gateway keeps them isolated by running one backend per client. Servers that later move to a
/// shared pool read it to scope their own state per caller.
/// </summary>
public static class McpCaller
{
    public const string HeaderName = "X-Mcp-Client";
    public const string Unknown = "default";

    private static IHttpContextAccessor? _accessor;

    internal static void Configure(IHttpContextAccessor accessor) => _accessor = accessor;

    public static string ClientId
    {
        get
        {
            HttpContext? context = _accessor?.HttpContext;
            if (context is null) return Unknown;

            string? value = context.Request.Headers[HeaderName].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value) ? Unknown : value;
        }
    }
}
```

- [ ] **Step 8: Implement the host builder**

Create `Libraries/Mcp.Hosting.Core/McpHttpHost.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

namespace Mcp.Hosting.Core;

public static class McpHttpHost
{
    /// <summary>
    /// The host shape every converted MCP server uses: loopback Kestrel on an OS-assigned port,
    /// logs at an absolute per-server path, and the options the gateway passes in.
    /// </summary>
    public static WebApplicationBuilder CreateBuilder(string[] args, string serverName)
    {
        McpHostOptions options = ReadOptions(args, serverName);

        Log.Logger = McpLoggingExtensions.SetupMcpLogging(LogPathFor(options.ServerName));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: false);

        builder.Services.AddSingleton(options);
        builder.Services.AddHttpContextAccessor();

        return builder;
    }

    /// <summary>%LOCALAPPDATA%\McpServers\logs\&lt;name&gt;\&lt;name&gt;-.log</summary>
    public static string LogPathFor(string serverName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "McpServers", "logs", serverName, $"{serverName}-.log");

    private static McpHostOptions ReadOptions(string[] args, string serverName) => new()
    {
        ServerName = Environment.GetEnvironmentVariable("MCP_SERVER_NAME") ?? serverName,
        PortFilePath = ReadArg(args, "--mcp-port-file"),
        ShutdownToken = Environment.GetEnvironmentVariable("MCP_SHUTDOWN_TOKEN"),
        Version = Environment.GetEnvironmentVariable("MCP_SERVER_VERSION") ?? "unknown"
    };

    private static string? ReadArg(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
```

- [ ] **Step 9: Implement `MapMcpHost`**

Create `Libraries/Mcp.Hosting.Core/McpHostApplicationExtensions.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mcp.Hosting.Core;

public static class McpHostApplicationExtensions
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public static WebApplication MapMcpHost(this WebApplication app)
    {
        McpHostOptions options = app.Services.GetRequiredService<McpHostOptions>();
        McpCaller.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

        app.MapMcp("/mcp");

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            name = options.ServerName,
            version = options.Version,
            pid = Environment.ProcessId,
            uptimeSeconds = (DateTimeOffset.UtcNow - StartedAt).TotalSeconds
        }));

        app.MapPost("/admin/shutdown", (HttpContext ctx, IHostApplicationLifetime lifetime) =>
        {
            if (!TokenMatches(options.ShutdownToken, ctx)) return Results.Unauthorized();

            lifetime.StopApplication();
            return Results.Accepted();
        });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            if (options.PortFilePath is null) return;

            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Mcp.Hosting.Core");

            try
            {
                int port = ResolveBoundPort(app);

                PortFile.WriteAsync(options.PortFilePath, port, Environment.ProcessId)
                    .GetAwaiter().GetResult();

                logger.LogInformation(
                    "{Server} listening on 127.0.0.1:{Port}, port file at {Path}",
                    options.ServerName, port, options.PortFilePath);
            }
            catch (Exception ex)
            {
                // Without a port file the gateway can never route to us, so failing loudly here is
                // better than idling as an unreachable process.
                logger.LogCritical(ex, "Could not write port file at {Path}", options.PortFilePath);
                throw;
            }
        });

        return app;
    }

    private static bool TokenMatches(string? expected, HttpContext ctx)
    {
        if (string.IsNullOrEmpty(expected)) return false;

        string? presented = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (presented is null || !presented.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(presented["Bearer ".Length..]));
    }

    private static int ResolveBoundPort(WebApplication app)
    {
        IServerAddressesFeature? feature = app.Services
            .GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();

        string? address = feature?.Addresses.FirstOrDefault();
        if (address is null)
        {
            throw new InvalidOperationException("Kestrel reported no bound address.");
        }

        return new Uri(address).Port;
    }
}
```

- [ ] **Step 10: Build and commit**

```powershell
dotnet build Libraries\Mcp.Hosting.Core\Mcp.Hosting.Core.csproj -c Debug -m:1 -v minimal
dotnet Libraries\Mcp.Hosting.Core.Tests\bin\Debug\net10.0\Mcp.Hosting.Core.Tests.dll -noColor
```

Expected: 0 warnings, 0 errors; 5 tests passed.

```bash
git add Libraries/Mcp.Hosting.Core Libraries/Mcp.Hosting.Core.Tests Directory.Packages.props McpServers.slnx
git commit -m "feat: add Mcp.Hosting.Core HTTP host shape for MCP servers"
```

---

### Task 3: Gateway skeleton — manifest, bearer token, status endpoint

**Files:**
- Create: `McpGateway/McpGateway.csproj`
- Create: `McpGateway/Program.cs`
- Create: `McpGateway/GatewayApp.cs`
- Create: `McpGateway/Configuration/ServerEntry.cs`
- Create: `McpGateway/Configuration/ManifestStore.cs`
- Create: `McpGateway/Security/TokenStore.cs`
- Create: `McpGateway/Security/BearerAuthMiddleware.cs`
- Create: `McpGateway.Tests/McpGateway.Tests.csproj`
- Create: `McpGateway.Tests/ManifestStoreTests.cs`
- Create: `McpGateway.Tests/TokenStoreTests.cs`
- Create: `servers.json`
- Modify: `McpServers.slnx`, `.gitignore`

**Interfaces:**
- Consumes: nothing from Task 2 (the gateway never references a server library).
- Produces:
  - `ServerEntry` with `Project`, `Assembly`, `DeployRoot`, `ActiveVersion`, `Pool`, `OverlapAllowed`, `EagerStart`, `IdleTimeoutMinutes`, `StartupTimeoutSeconds`, and computed `IsShared`
  - `ManifestStore.Load(string path) -> ManifestStore`; `.Entries -> IReadOnlyDictionary<string, ServerEntry>`; `.TryGet(string, out ServerEntry?) -> bool`; `.SetActiveVersionAsync(string, string, CancellationToken) -> Task`
  - `TokenStore.GetOrCreate(string path) -> string`
  - `GatewayBuildOptions` — `record { string ManifestPath, string TokenPath, string RepoRoot, string Url }`
  - `GatewayApp.Build(GatewayBuildOptions) -> WebApplication` and `GatewayApp.DefaultOptions(string repoRoot) -> GatewayBuildOptions`. Tasks 4–9 extend `Build`.

- [ ] **Step 1: Create the projects**

Create `McpGateway/McpGateway.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <RootNamespace>McpGateway</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Yarp.ReverseProxy" />
        <PackageReference Include="Serilog" />
        <PackageReference Include="Serilog.Extensions.Logging" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Libraries\SerilogFileWriter\SerilogFileWriter.csproj" />
    </ItemGroup>

</Project>
```

Task 4 adds a `ProjectReference` to `Libraries/Mcp.Hosting.Core` here as well, for `PortFile` and
`McpHttpHost.LogPathFor`. Adding it now is harmless and saves an edit later.

Create `McpGateway.Tests/McpGateway.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" />
        <PackageReference Include="xunit.runner.visualstudio">
          <PrivateAssets>all</PrivateAssets>
          <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\McpGateway\McpGateway.csproj" />
    </ItemGroup>

</Project>
```

Add both to `McpServers.slnx` beside the other server projects:

```xml
  <Project Path="McpGateway\McpGateway.csproj" />
  <Project Path="McpGateway.Tests\McpGateway.Tests.csproj" />
```

**Ordering note:** `Microsoft.NET.Sdk.Web` will not build without an entry point, and the test
project references the gateway. So create `Program.cs` and `GatewayApp.cs` (Step 9's listings) and
`BearerAuthMiddleware.cs` (Step 8's) as soon as the projects exist, before Step 3 tries to build.
Otherwise Step 3 fails for a missing entry point rather than the missing types it is meant to prove
absent. The code is unchanged — only the order it lands in.

- [ ] **Step 2: Write the failing manifest tests**

Create `McpGateway.Tests/ManifestStoreTests.cs`:

```csharp
using McpGateway.Configuration;
using Xunit;

namespace McpGateway.Tests;

public sealed class ManifestStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-manifest-" + Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "servers.json");

    public ManifestStoreTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ManifestPath, """
        {
          "code-assist": {
            "project": "CodeAssistMcp/CodeAssistMcp.csproj",
            "assembly": "CodeAssistMcp.dll",
            "deployRoot": "deploy/code-assist",
            "activeVersion": "v-146874c-20260830T1214",
            "pool": "shared",
            "overlapAllowed": false,
            "eagerStart": true,
            "idleTimeoutMinutes": 0,
            "startupTimeoutSeconds": 120
          },
          "sql": {
            "project": "SqlMcp/SqlMcp.csproj",
            "assembly": "SqlMcp.dll",
            "deployRoot": "deploy/sql",
            "activeVersion": "v-146874c-20260830T1214",
            "pool": "per-client",
            "overlapAllowed": true,
            "eagerStart": false,
            "idleTimeoutMinutes": 30,
            "startupTimeoutSeconds": 30
          }
        }
        """);
    }

    [Fact]
    public void Load_ReadsEveryEntry()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        Assert.Equal(2, store.Entries.Count);
        Assert.True(store.TryGet("code-assist", out ServerEntry? codeAssist));
        Assert.Equal("shared", codeAssist!.Pool);
        Assert.True(codeAssist.IsShared);
        Assert.False(codeAssist.OverlapAllowed);
        Assert.True(codeAssist.EagerStart);
        Assert.Equal(0, codeAssist.IdleTimeoutMinutes);
        Assert.Equal(120, codeAssist.StartupTimeoutSeconds);
    }

    [Fact]
    public void Load_MarksPerClientEntriesAsNotShared()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.False(sql!.IsShared);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownServer()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        Assert.False(store.TryGet("nope", out _));
    }

    [Fact]
    public async Task SetActiveVersionAsync_PersistsToDisk()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        await store.SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        ManifestStore reloaded = ManifestStore.Load(ManifestPath);
        Assert.True(reloaded.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-abc1234-20260901T0900", sql!.ActiveVersion);
        Assert.Equal("per-client", sql.Pool);
    }

    [Fact]
    public async Task SetActiveVersionAsync_UpdatesTheInMemoryView()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        await store.SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-abc1234-20260901T0900", sql!.ActiveVersion);
    }

    [Fact]
    public async Task SetActiveVersionAsync_Throws_ForUnknownServer()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.SetActiveVersionAsync(
                "nope", "v-1", TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

Create `McpGateway.Tests/TokenStoreTests.cs`:

```csharp
using McpGateway.Security;
using Xunit;

namespace McpGateway.Tests;

public sealed class TokenStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-token-" + Guid.NewGuid().ToString("N"));

    private string TokenPath => Path.Combine(_directory, "token");

    [Fact]
    public void GetOrCreate_GeneratesOnFirstCall()
    {
        string token = TokenStore.GetOrCreate(TokenPath);

        Assert.True(File.Exists(TokenPath));
        Assert.True(token.Length >= 40, $"token was only {token.Length} chars");
    }

    [Fact]
    public void GetOrCreate_IsStableAcrossCalls()
    {
        string first = TokenStore.GetOrCreate(TokenPath);
        string second = TokenStore.GetOrCreate(TokenPath);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetOrCreate_GeneratesDistinctTokensForDistinctPaths()
    {
        string first = TokenStore.GetOrCreate(TokenPath);
        string second = TokenStore.GetOrCreate(Path.Combine(_directory, "other"));

        Assert.NotEqual(first, second);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

- [ ] **Step 3: Run to verify they fail**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `ManifestStore`, `ServerEntry` and `TokenStore` do not exist.

- [ ] **Step 4: Implement the manifest**

Create `McpGateway/Configuration/ServerEntry.cs`:

```csharp
using System.Text.Json.Serialization;

namespace McpGateway.Configuration;

public sealed record ServerEntry
{
    [JsonPropertyName("project")] public required string Project { get; init; }
    [JsonPropertyName("assembly")] public required string Assembly { get; init; }
    [JsonPropertyName("deployRoot")] public required string DeployRoot { get; init; }
    [JsonPropertyName("activeVersion")] public required string ActiveVersion { get; init; }

    /// <summary>"shared" gives every caller one backend; "per-client" gives each its own.</summary>
    [JsonPropertyName("pool")] public string Pool { get; init; } = "per-client";

    /// <summary>False for servers whose machine-wide state two live instances would corrupt.</summary>
    [JsonPropertyName("overlapAllowed")] public bool OverlapAllowed { get; init; } = true;

    [JsonPropertyName("eagerStart")] public bool EagerStart { get; init; }
    [JsonPropertyName("idleTimeoutMinutes")] public int IdleTimeoutMinutes { get; init; } = 30;
    [JsonPropertyName("startupTimeoutSeconds")] public int StartupTimeoutSeconds { get; init; } = 30;

    [JsonIgnore]
    public bool IsShared => string.Equals(Pool, "shared", StringComparison.OrdinalIgnoreCase);
}
```

Create `McpGateway/Configuration/ManifestStore.cs`:

```csharp
using System.Text.Json;

namespace McpGateway.Configuration;

/// <summary>
/// servers.json is the source of truth for which version is active. Deliberately a file the
/// gateway rewrites rather than a directory junction: rollback is one field, and Windows never has
/// to retarget a path with open handles underneath it.
/// </summary>
public sealed class ManifestStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile Dictionary<string, ServerEntry> _entries;

    private ManifestStore(string path, Dictionary<string, ServerEntry> entries)
    {
        _path = path;
        _entries = entries;
    }

    public IReadOnlyDictionary<string, ServerEntry> Entries => _entries;

    public static ManifestStore Load(string path)
    {
        Dictionary<string, ServerEntry> entries =
            JsonSerializer.Deserialize<Dictionary<string, ServerEntry>>(
                File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Manifest at {path} deserialized to null.");

        return new ManifestStore(path, new Dictionary<string, ServerEntry>(
            entries, StringComparer.OrdinalIgnoreCase));
    }

    public bool TryGet(string name, out ServerEntry? entry) => _entries.TryGetValue(name, out entry);

    public async Task SetActiveVersionAsync(
        string name, string version, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!_entries.TryGetValue(name, out ServerEntry? entry))
            {
                throw new KeyNotFoundException($"No server named '{name}' in the manifest.");
            }

            var updated = new Dictionary<string, ServerEntry>(
                _entries, StringComparer.OrdinalIgnoreCase)
            {
                [name] = entry with { ActiveVersion = version }
            };

            string temp = _path + ".tmp";
            await File.WriteAllTextAsync(
                temp, JsonSerializer.Serialize(updated, Options), cancellationToken);
            File.Move(temp, _path, overwrite: true);

            _entries = updated;
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
```

- [ ] **Step 5: Implement the token store**

Create `McpGateway/Security/TokenStore.cs`:

```csharp
using System.Security.Cryptography;

namespace McpGateway.Security;

public static class TokenStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Reads the bearer token, generating one on first run. Loopback HTTP is reachable by every
    /// process on the machine, so this is the only thing stopping any of them from driving
    /// desktop-commander or ssh-mcp.
    /// </summary>
    public static string GetOrCreate(string path)
    {
        Gate.Wait();
        try
        {
            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path).Trim();
                if (existing.Length > 0) return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string token = Base64Url(RandomNumberGenerator.GetBytes(32));

            string temp = path + ".tmp";
            File.WriteAllText(temp, token);
            File.Move(temp, path, overwrite: true);

            return token;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: 9 passed.

- [ ] **Step 7: Verify the tests can fail**

Comment out `_entries = updated;` in `SetActiveVersionAsync` and rerun. Expected: `SetActiveVersionAsync_UpdatesTheInMemoryView` goes red, `SetActiveVersionAsync_PersistsToDisk` stays green — which is exactly the split those two tests exist to draw. Restore it.

Now comment out the `File.Move(temp, _path, overwrite: true);` line and rerun. Expected: `SetActiveVersionAsync_PersistsToDisk` goes red. Restore it.

In `TokenStore.GetOrCreate`, replace `RandomNumberGenerator.GetBytes(32)` with `new byte[32]` and rerun. Expected: `GeneratesDistinctTokensForDistinctPaths` goes red. Restore it.

- [ ] **Step 8: Implement the auth middleware**

Create `McpGateway/Security/BearerAuthMiddleware.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace McpGateway.Security;

public sealed class BearerAuthMiddleware(RequestDelegate next, string expectedToken)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(expectedToken);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAuthorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsync("Missing or invalid bearer token.");
            return;
        }

        await next(context);
    }

    private bool IsAuthorized(HttpContext context)
    {
        string? presented = context.Request.Headers.Authorization.FirstOrDefault();
        if (presented is null || !presented.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            _expected, Encoding.UTF8.GetBytes(presented["Bearer ".Length..]));
    }
}
```

- [ ] **Step 9: Implement the composition root**

`GatewayApp.Build` is a static factory rather than a `Program`-based `WebApplicationFactory` so Task 7's integration test can stand a gateway up in-process without test-host plumbing.

Create `McpGateway/GatewayApp.cs`:

```csharp
using McpGateway.Configuration;
using McpGateway.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

namespace McpGateway;

public sealed record GatewayBuildOptions
{
    public required string ManifestPath { get; init; }
    public required string TokenPath { get; init; }
    public required string RepoRoot { get; init; }
    public string Url { get; init; } = "http://127.0.0.1:7300";
}

public static class GatewayApp
{
    public static GatewayBuildOptions DefaultOptions(string repoRoot) => new()
    {
        ManifestPath = Path.Combine(repoRoot, "servers.json"),
        TokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpGateway", "token"),
        RepoRoot = repoRoot
    };

    public static WebApplication Build(GatewayBuildOptions options)
    {
        Log.Logger = McpLoggingExtensions.SetupMcpLogging(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpServers", "logs", "gateway", "gateway-.log"));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(options.Url);
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: false);

        string token = TokenStore.GetOrCreate(options.TokenPath);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(ManifestStore.Load(options.ManifestPath));

        WebApplication app = builder.Build();

        app.UseMiddleware<BearerAuthMiddleware>(token);

        app.MapGet("/admin/servers", (ManifestStore manifest) => Results.Json(
            manifest.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    pair.Value.Pool,
                    pair.Value.ActiveVersion,
                    pair.Value.OverlapAllowed,
                    pair.Value.EagerStart
                })));

        return app;
    }
}
```

Create `McpGateway/Program.cs`:

```csharp
using McpGateway;

string repoRoot = Environment.GetEnvironmentVariable("MCP_GATEWAY_REPO_ROOT")
    ?? Directory.GetCurrentDirectory();

await GatewayApp.Build(GatewayApp.DefaultOptions(repoRoot)).RunAsync();
```

- [ ] **Step 10: Create the manifest and ignore the deploy tree**

Create `servers.json` at the repo root. CodeAssist only — Task 13 fills in its real version, Stage 3 adds the other thirteen:

```json
{
  "code-assist": {
    "project": "CodeAssistMcp/CodeAssistMcp.csproj",
    "assembly": "CodeAssistMcp.dll",
    "deployRoot": "deploy/code-assist",
    "activeVersion": "unset",
    "pool": "shared",
    "overlapAllowed": false,
    "eagerStart": true,
    "idleTimeoutMinutes": 0,
    "startupTimeoutSeconds": 120
  }
}
```

```bash
printf 'deploy/\n' >> .gitignore
```

- [ ] **Step 11: Verify the gateway runs and rejects unauthenticated calls**

```powershell
$env:MCP_GATEWAY_REPO_ROOT = "C:\Users\jorda\RiderProjects\McpServers"
dotnet run --project McpGateway\McpGateway.csproj
```

In a second shell:

```powershell
# no token -> 401
try { Invoke-WebRequest http://127.0.0.1:7300/admin/servers -UseBasicParsing } catch { $_.Exception.Response.StatusCode }

# with token -> the manifest
$t = Get-Content "$env:LOCALAPPDATA\McpGateway\token"
Invoke-RestMethod http://127.0.0.1:7300/admin/servers -Headers @{ Authorization = "Bearer $t" }
```

Expected: `Unauthorized` for the first, and a `code-assist` object with `pool` of `shared` for the second. Stop the gateway with Ctrl-C.

- [ ] **Step 12: Commit**

```bash
git add McpGateway McpGateway.Tests servers.json McpServers.slnx .gitignore
git commit -m "feat: add gateway skeleton with manifest store and bearer auth"
```

---

### Task 4: Backend supervisor — lazy start, health gate, concurrent waiters

The heart of the gateway. A request for a server that isn't running blocks until that server is
healthy, rather than getting a 503 for being early. Concurrent requests for the same backend share
one start.

**Files:**
- Create: `McpGateway/Supervision/BackendKey.cs`
- Create: `McpGateway/Supervision/IBackendLauncher.cs`
- Create: `McpGateway/Supervision/ProcessBackendLauncher.cs`
- Create: `McpGateway/Supervision/BackendInstance.cs`
- Create: `McpGateway/Supervision/HealthProbe.cs`
- Create: `McpGateway/Supervision/BackendStartupException.cs`
- Create: `McpGateway/Supervision/BackendSupervisor.cs`
- Create: `McpGateway.Tests/FakeBackendLauncher.cs`
- Create: `McpGateway.Tests/BackendSupervisorTests.cs`
- Modify: `McpGateway/GatewayApp.cs`

**Interfaces:**
- Consumes: `ManifestStore`, `ServerEntry`, `GatewayBuildOptions` (Task 3); the `--mcp-port-file` and `MCP_SERVER_VERSION`/`MCP_SHUTDOWN_TOKEN` contract from Task 2.
- Produces:
  - `BackendKey` — `readonly record struct(string Server, string PoolKey)`
  - `BackendLaunchRequest` — `record(string ServerName, string Version, string AssemblyPath, string PortFilePath, string ShutdownToken)`
  - `IBackendLauncher.Start(BackendLaunchRequest) -> IBackendHandle`
  - `IBackendHandle` — `int ProcessId`, `bool HasExited`, `IAsyncDisposable`
  - `BackendInstance` — `Key`, `Version`, `Port`, `DestinationPrefix`, `LastUsedAt`, `InFlight`, `BeginRequest() -> IDisposable`, `WaitForDrainAsync(TimeSpan, CancellationToken) -> Task<bool>`, `StopAsync(CancellationToken) -> Task`
  - `BackendSupervisor.GetOrStartAsync(BackendKey, CancellationToken) -> Task<BackendInstance>`, `.StartDetachedAsync(BackendKey, string version, CancellationToken) -> Task<BackendInstance>`, `.TryGet(BackendKey, out BackendInstance?) -> bool`, `.Replace(BackendKey, BackendInstance) -> BackendInstance?`, `.StopAsync(BackendKey, CancellationToken) -> Task`, `.All -> IReadOnlyCollection<BackendInstance>`
  - `BackendStartupException(string message, string logTail)`
  - Task 5 forwards to `BackendInstance.DestinationPrefix`; Tasks 6–8 use `Replace`, `StartDetachedAsync`, `WaitForDrainAsync` and `LastUsedAt`.

- [ ] **Step 1: Write the failing supervisor tests**

Create `McpGateway.Tests/FakeBackendLauncher.cs` — an in-process stand-in that behaves like a real backend, so unit tests never spawn `dotnet`:

```csharp
using McpGateway.Supervision;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpGateway.Tests;

/// <summary>
/// Starts a real loopback Kestrel that answers /health and echoes its version, then writes the
/// port file exactly as Mcp.Hosting.Core does. Lets the supervisor be tested against the real
/// port-file-then-health-gate handshake without process spawning.
/// </summary>
public sealed class FakeBackendLauncher : IBackendLauncher
{
    private int _nextPid = 1000;

    /// <summary>Set to skip writing the port file, simulating a backend that never comes up.</summary>
    public bool SuppressPortFile { get; set; }

    /// <summary>Set to make /health return 500, simulating a backend that starts unhealthy.</summary>
    public bool Unhealthy { get; set; }

    /// <summary>
    /// Makes starts unhealthy from this start number onward, counting every Start call from 1.
    /// Unlike <see cref="Unhealthy"/>, which fails every start including the first, this lets a
    /// test bring earlier backends up healthy and fail a later one -- the case where an activation
    /// has to undo replacements it already started.
    /// </summary>
    public int UnhealthyFromStartNumber { get; set; } = int.MaxValue;

    /// <summary>
    /// Delays the port file so a test can act while a start is genuinely in flight. It must delay
    /// the port file rather than Start itself: Start runs synchronously inside Lazy.Value on the
    /// caller's thread, so a sleep here would be spent before GetOrStartAsync ever returns a Task,
    /// leaving nothing in flight to cancel into.
    /// </summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.Zero;

    public int StartCount { get; private set; }

    private readonly List<IBackendHandle> _handles = [];

    /// <summary>
    /// Every handle Start has returned, in call order (index 0 is the first call). Lets a test
    /// confirm a specific start was later torn down -- e.g. a replacement that came up healthy but
    /// was then stopped because a sibling start in the same activation failed -- without the
    /// production code needing to expose it anywhere.
    /// </summary>
    public IReadOnlyList<IBackendHandle> Handles => _handles;

    public IBackendHandle Start(BackendLaunchRequest request)
    {
        StartCount++;

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        WebApplication app = builder.Build();

        bool unhealthy = Unhealthy || StartCount >= UnhealthyFromStartNumber;
        app.MapGet("/health", () => unhealthy
            ? Results.StatusCode(500)
            : Results.Json(new { status = "ok", version = request.Version }));

        app.MapPost("/mcp", (HttpContext ctx) => Results.Json(new
        {
            version = request.Version,
            clientHeader = ctx.Request.Headers["X-Mcp-Client"].FirstOrDefault(),
            authHeader = ctx.Request.Headers.Authorization.FirstOrDefault(),
            query = ctx.Request.QueryString.Value
        }));
        app.MapPost("/admin/shutdown", () => Results.Accepted());

        app.Start();

        int port = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

        int pid = _nextPid++;

        if (!SuppressPortFile)
        {
            if (StartDelay > TimeSpan.Zero)
            {
                // Write it late and asynchronously, so the supervisor sits in WaitForPortFileAsync
                // for the duration -- a real in-flight window a test can cancel or stop into.
                TimeSpan delay = StartDelay;
                string path = request.PortFilePath;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay);
                    await PortFile.WriteAsync(path, port, pid);
                });
            }
            else
            {
                PortFile.WriteAsync(request.PortFilePath, port, pid).GetAwaiter().GetResult();
            }
        }

        return new FakeHandle(app, pid);
    }

    private sealed class FakeHandle(WebApplication app, int pid) : IBackendHandle
    {
        public int ProcessId { get; } = pid;
        public bool HasExited { get; private set; }

        public async ValueTask DisposeAsync()
        {
            if (HasExited) return;
            HasExited = true;
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
```

Create `McpGateway.Tests/BackendSupervisorTests.cs`:

```csharp
using McpGateway.Configuration;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class BackendSupervisorTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-supervisor-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly BackendSupervisor _supervisor;

    public BackendSupervisorTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "demo": {
            "project": "Demo/Demo.csproj",
            "assembly": "Demo.dll",
            "deployRoot": "deploy/demo",
            "activeVersion": "v-one",
            "pool": "per-client",
            "startupTimeoutSeconds": 10
          }
        }
        """);

        _supervisor = new BackendSupervisor(
            ManifestStore.Load(manifestPath),
            _launcher,
            new HealthProbe(new HttpClient()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                RepoRoot = _root
            },
            "shutdown-token",
            NullLogger<BackendSupervisor>.Instance);
    }

    [Fact]
    public async Task GetOrStartAsync_StartsAndReturnsAHealthyBackend()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        Assert.True(instance.Port > 0);
        Assert.Equal("v-one", instance.Version);
        Assert.Equal($"http://127.0.0.1:{instance.Port}", instance.DestinationPrefix);
    }

    [Fact]
    public async Task GetOrStartAsync_ReusesTheSameBackendForTheSameKey()
    {
        var key = new BackendKey("demo", "code");

        BackendInstance first = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);
        BackendInstance second = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(1, _launcher.StartCount);
    }

    [Fact]
    public async Task GetOrStartAsync_GivesDistinctBackendsToDistinctPoolKeys()
    {
        BackendInstance code = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);
        BackendInstance desktop = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "desktop"), TestContext.Current.CancellationToken);

        Assert.NotSame(code, desktop);
        Assert.NotEqual(code.Port, desktop.Port);
        Assert.Equal(2, _launcher.StartCount);
    }

    [Fact]
    public async Task GetOrStartAsync_ConcurrentCallersShareOneStart()
    {
        var key = new BackendKey("demo", "code");

        BackendInstance[] results = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ =>
                _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken)));

        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.Equal(1, _launcher.StartCount);
    }

    [Fact]
    public async Task GetOrStartAsync_ThrowsWithLogTail_WhenThePortFileNeverArrives()
    {
        _launcher.SuppressPortFile = true;

        BackendStartupException ex = await Assert.ThrowsAsync<BackendStartupException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("demo", "code"), TestContext.Current.CancellationToken));

        Assert.Contains("port file", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetOrStartAsync_Throws_WhenTheBackendIsUnhealthy()
    {
        _launcher.Unhealthy = true;

        await Assert.ThrowsAsync<BackendStartupException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("demo", "code"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetOrStartAsync_RetriesAfterAFailedStart()
    {
        _launcher.SuppressPortFile = true;
        await Assert.ThrowsAsync<BackendStartupException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("demo", "code"), TestContext.Current.CancellationToken));

        _launcher.SuppressPortFile = false;
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        Assert.True(instance.Port > 0);
    }

    [Fact]
    public async Task GetOrStartAsync_Throws_ForAServerNotInTheManifest()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _supervisor.GetOrStartAsync(
                new BackendKey("nope", "code"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BeginRequest_TracksInFlightAndDrains()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        Assert.Equal(0, instance.InFlight);

        IDisposable lease = instance.BeginRequest();
        Assert.Equal(1, instance.InFlight);

        Task<bool> drain = instance.WaitForDrainAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(drain.IsCompleted);

        lease.Dispose();

        Assert.True(await drain);
        Assert.Equal(0, instance.InFlight);
    }

    [Fact]
    public async Task WaitForDrainAsync_StaysPending_UntilEveryOverlappingRequestFinishes()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        IDisposable first = instance.BeginRequest();
        IDisposable second = instance.BeginRequest();
        Assert.Equal(2, instance.InFlight);

        first.Dispose();
        Assert.Equal(1, instance.InFlight);

        // One request is still in flight, so a short drain must time out. A drain that signals
        // here would let an upgrade kill a backend mid-request -- the exact failure the
        // zero-downtime swap exists to prevent.
        Assert.False(await instance.WaitForDrainAsync(
            TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken));

        second.Dispose();

        Assert.True(await instance.WaitForDrainAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(0, instance.InFlight);
    }

    [Fact]
    public async Task WaitForDrainAsync_ReturnsFalse_WhenRequestsOutlastTheTimeout()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("demo", "code"), TestContext.Current.CancellationToken);

        using IDisposable lease = instance.BeginRequest();

        Assert.False(await instance.WaitForDrainAsync(
            TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `BackendSupervisor`, `BackendKey`, `HealthProbe`, `BackendInstance`, `IBackendLauncher`, `BackendStartupException` do not exist.

The test project also needs the hosting library for `PortFile`. Add to `McpGateway.Tests/McpGateway.Tests.csproj`:

```xml
        <ProjectReference Include="..\Libraries\Mcp.Hosting.Core\Mcp.Hosting.Core.csproj" />
```

- [ ] **Step 3: Implement the small types**

Create `McpGateway/Supervision/BackendKey.cs`:

```csharp
namespace McpGateway.Supervision;

/// <summary>
/// Identifies one backend process. PoolKey is empty for a shared server and the calling client's
/// id for a per-client one — that difference is the whole isolation model.
/// </summary>
public readonly record struct BackendKey(string Server, string PoolKey)
{
    public override string ToString() =>
        PoolKey.Length == 0 ? Server : $"{Server}[{PoolKey}]";
}
```

Create `McpGateway/Supervision/BackendStartupException.cs`:

```csharp
namespace McpGateway.Supervision;

public sealed class BackendStartupException(string message, string logTail)
    : Exception(message)
{
    /// <summary>Tail of the backend's log, so a 503 can say why rather than just that.</summary>
    public string LogTail { get; } = logTail;
}
```

Create `McpGateway/Supervision/IBackendLauncher.cs`:

```csharp
namespace McpGateway.Supervision;

public sealed record BackendLaunchRequest(
    string ServerName,
    string Version,
    string AssemblyPath,
    string PortFilePath,
    string ShutdownToken);

public interface IBackendLauncher
{
    IBackendHandle Start(BackendLaunchRequest request);
}

public interface IBackendHandle : IAsyncDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
}
```

Create `McpGateway/Supervision/HealthProbe.cs`:

```csharp
namespace McpGateway.Supervision;

public sealed class HealthProbe(HttpClient client)
{
    public async Task<bool> WaitUntilHealthyAsync(
        int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        var uri = new Uri($"http://127.0.0.1:{port}/health");

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpResponseMessage response = await client.GetAsync(uri, cancellationToken);
                if (response.IsSuccessStatusCode) return true;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(100, cancellationToken);
        }

        return false;
    }
}
```

- [ ] **Step 4: Implement `BackendInstance`**

Create `McpGateway/Supervision/BackendInstance.cs`:

```csharp
namespace McpGateway.Supervision;

public sealed class BackendInstance(
    BackendKey key,
    string version,
    int port,
    IBackendHandle handle,
    string shutdownToken)
{
    private readonly object _gate = new();
    private TaskCompletionSource _drained = CreateDrainedSource(signalled: true);
    private int _inFlight;

    public BackendKey Key { get; } = key;
    public string Version { get; } = version;
    public int Port { get; } = port;
    public IBackendHandle Handle { get; } = handle;
    public string DestinationPrefix { get; } = $"http://127.0.0.1:{port}";
    public DateTimeOffset LastUsedAt { get; private set; } = DateTimeOffset.UtcNow;

    public int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>Marks a request as in flight. Disposing the lease releases it.</summary>
    public IDisposable BeginRequest()
    {
        lock (_gate)
        {
            if (_inFlight++ == 0) _drained = CreateDrainedSource(signalled: false);
            LastUsedAt = DateTimeOffset.UtcNow;
        }

        return new Lease(this);
    }

    /// <summary>True if the backend went quiet within the timeout; false if requests outlasted it.</summary>
    public async Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task drained;
        lock (_gate) { drained = _drained.Task; }

        Task finished = await Task.WhenAny(drained, Task.Delay(timeout, cancellationToken));
        return ReferenceEquals(finished, drained);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{DestinationPrefix}/admin/shutdown");
            request.Headers.Add("Authorization", $"Bearer {shutdownToken}");

            await client.SendAsync(request, cancellationToken);
        }
        catch (Exception)
        {
            // A backend that won't answer its shutdown endpoint still gets disposed below.
        }

        await Handle.DisposeAsync();
    }

    private void Release()
    {
        lock (_gate)
        {
            if (--_inFlight == 0) _drained.TrySetResult();
            LastUsedAt = DateTimeOffset.UtcNow;
        }
    }

    private static TaskCompletionSource CreateDrainedSource(bool signalled)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (signalled) source.SetResult();
        return source;
    }

    private sealed class Lease(BackendInstance owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) owner.Release();
        }
    }
}
```

- [ ] **Step 5: Implement the supervisor**

Create `McpGateway/Supervision/BackendSupervisor.cs`:

```csharp
using System.Collections.Concurrent;
using McpGateway.Configuration;
using Mcp.Hosting.Core;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

public sealed class BackendSupervisor(
    ManifestStore manifest,
    IBackendLauncher launcher,
    HealthProbe healthProbe,
    GatewayBuildOptions options,
    string shutdownToken,
    ILogger<BackendSupervisor> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<BackendKey, Lazy<Task<BackendInstance>>> _pool = new();

    public IReadOnlyCollection<BackendInstance> All => _pool.Values
        .Where(entry => entry.IsValueCreated && entry.Value.IsCompletedSuccessfully)
        .Select(entry => entry.Value.Result)
        .ToList();

    /// <summary>
    /// Returns the running backend for this key, starting it if needed. Concurrent callers for the
    /// same key await the same start rather than racing to spawn duplicates.
    /// </summary>
    public async Task<BackendInstance> GetOrStartAsync(
        BackendKey key, CancellationToken cancellationToken)
    {
        while (true)
        {
            Lazy<Task<BackendInstance>> entry = _pool.GetOrAdd(key, k => new Lazy<Task<BackendInstance>>(
                () => StartAsync(k, ResolveEntry(k.Server).ActiveVersion, CancellationToken.None)));

            try
            {
                BackendInstance instance = await entry.Value.WaitAsync(cancellationToken);

                // A crashed backend is evicted and restarted on the next request rather than
                // handed out dead.
                if (instance.Handle.HasExited)
                {
                    RemoveIfSame(key, entry);
                    continue;
                }

                return instance;
            }
            catch
            {
                // Evict only a genuinely failed start, so the key is not poisoned forever.
                // Task.WaitAsync throws when THIS caller's token fires while the shared start is
                // still running — the start itself is unaffected and will finish. Evicting on that
                // would hand the next caller a duplicate spawn and orphan the process still coming
                // up, with nothing holding a handle to stop it.
                if (entry.Value.IsFaulted) RemoveIfSame(key, entry);
                throw;
            }
        }
    }

    /// <summary>Starts a backend that the pool does not own, for a blue/green swap.</summary>
    public Task<BackendInstance> StartDetachedAsync(
        BackendKey key, string version, CancellationToken cancellationToken) =>
        StartAsync(key, version, cancellationToken);

    public bool TryGet(BackendKey key, out BackendInstance? instance)
    {
        instance = null;

        if (!_pool.TryGetValue(key, out Lazy<Task<BackendInstance>>? entry)) return false;
        if (!entry.IsValueCreated || !entry.Value.IsCompletedSuccessfully) return false;

        instance = entry.Value.Result;
        return true;
    }

    /// <summary>Swaps in an already-started backend. Returns the one it displaced, if any.</summary>
    public BackendInstance? Replace(BackendKey key, BackendInstance instance)
    {
        TryGet(key, out BackendInstance? previous);

        // Lazy(T value) is already-created, so IsValueCreated is true immediately. A deferred
        // factory here would make the swapped-in backend invisible to TryGet, All and StopAsync,
        // which all short-circuit on !IsValueCreated -- the blue/green swap would install a live
        // backend that /admin/servers never lists and the idle reaper never reaps.
        _pool[key] = new Lazy<Task<BackendInstance>>(Task.FromResult(instance));

        return previous;
    }

    public async Task StopAsync(BackendKey key, CancellationToken cancellationToken)
    {
        if (!_pool.TryRemove(key, out Lazy<Task<BackendInstance>>? entry)) return;
        if (!entry.IsValueCreated) return;

        BackendInstance instance;
        try
        {
            // A start still in flight has to be awaited rather than abandoned: its process would
            // otherwise finish coming up with nothing left holding a handle to stop it.
            instance = await entry.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The caller gave up waiting -- a slow server's startup timeout must not pin a
            // shutdown. Hand the teardown to a continuation so the process is still stopped when
            // the start finally lands, instead of leaking it.
            _ = entry.Value.ContinueWith(
                started => started.Result.StopAsync(CancellationToken.None),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
            return;
        }
        catch (Exception)
        {
            // The start failed, so there is no process to stop.
            return;
        }

        await instance.StopAsync(cancellationToken);
    }

    public ServerEntry ResolveEntry(string server) =>
        manifest.TryGet(server, out ServerEntry? entry)
            ? entry!
            : throw new KeyNotFoundException($"No server named '{server}' in the manifest.");

    private async Task<BackendInstance> StartAsync(
        BackendKey key, string version, CancellationToken cancellationToken)
    {
        ServerEntry entry = ResolveEntry(key.Server);

        string assemblyPath = Path.Combine(
            options.RepoRoot, entry.DeployRoot, version, entry.Assembly);

        string portFilePath = Path.Combine(Path.GetTempPath(), "mcp-gateway-ports",
            $"{key.Server}-{Guid.NewGuid():N}.json");

        logger.LogInformation("Starting {Key} version {Version}", key, version);

        IBackendHandle handle = launcher.Start(new BackendLaunchRequest(
            key.Server, version, assemblyPath, portFilePath, shutdownToken));

        var timeout = TimeSpan.FromSeconds(entry.StartupTimeoutSeconds);

        try
        {
            PortFileContent port = await WaitForPortFileAsync(
                portFilePath, handle, timeout, cancellationToken);

            if (!await healthProbe.WaitUntilHealthyAsync(port.Port, timeout, cancellationToken))
            {
                throw new BackendStartupException(
                    $"{key} started on port {port.Port} but never reported healthy within {timeout}.",
                    ReadLogTail(key.Server));
            }

            logger.LogInformation(
                "{Key} healthy on port {Port} (pid {Pid})", key, port.Port, port.Pid);

            return new BackendInstance(key, version, port.Port, handle, shutdownToken);
        }
        catch
        {
            await handle.DisposeAsync();
            throw;
        }
        finally
        {
            try { File.Delete(portFilePath); } catch (IOException) { }
        }
    }

    private static async Task<PortFileContent> WaitForPortFileAsync(
        string path, IBackendHandle handle, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (PortFile.TryRead(path, out PortFileContent content)) return content;

            if (handle.HasExited)
            {
                throw new BackendStartupException(
                    "Backend exited before writing its port file.", string.Empty);
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new BackendStartupException(
            $"Backend did not write its port file at {path} within {timeout}.", string.Empty);
    }

    private static string ReadLogTail(string serverName)
    {
        try
        {
            string directory = Path.GetDirectoryName(McpHttpHost.LogPathFor(serverName))!;
            string? newest = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.log")
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;

            if (newest is null) return string.Empty;

            using var stream = new FileStream(
                newest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            return string.Join(Environment.NewLine,
                reader.ReadToEnd().Split(Environment.NewLine).TakeLast(20));
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private void RemoveIfSame(BackendKey key, Lazy<Task<BackendInstance>> entry) =>
        // Atomic compare-and-remove. The check-then-act form could delete a healthy replacement
        // that Replace() installed between the read and the remove — and Replace is exactly what
        // the blue/green swap in later tasks uses.
        _pool.TryRemove(new KeyValuePair<BackendKey, Lazy<Task<BackendInstance>>>(key, entry));

    public async ValueTask DisposeAsync()
    {
        // Bounded: without this a backend still inside its startup timeout (120s for code-assist)
        // would pin the whole gateway shutdown.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        foreach (BackendKey key in _pool.Keys.ToList())
        {
            await StopAsync(key, timeout.Token);
        }
    }
}
```

- [ ] **Step 6: Implement the real process launcher**

Create `McpGateway/Supervision/ProcessBackendLauncher.cs`:

```csharp
using System.Diagnostics;

namespace McpGateway.Supervision;

public sealed class ProcessBackendLauncher : IBackendLauncher
{
    public IBackendHandle Start(BackendLaunchRequest request)
    {
        if (!File.Exists(request.AssemblyPath))
        {
            throw new BackendStartupException(
                $"No assembly at {request.AssemblyPath}. Publish the version first.",
                string.Empty);
        }

        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(request.AssemblyPath)!,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        info.ArgumentList.Add(request.AssemblyPath);
        info.ArgumentList.Add("--mcp-port-file");
        info.ArgumentList.Add(request.PortFilePath);

        info.Environment["MCP_SERVER_NAME"] = request.ServerName;
        info.Environment["MCP_SERVER_VERSION"] = request.Version;
        info.Environment["MCP_SHUTDOWN_TOKEN"] = request.ShutdownToken;

        Process process = Process.Start(info)
            ?? throw new BackendStartupException(
                $"Could not start {request.AssemblyPath}.", string.Empty);

        return new ProcessHandle(process);
    }

    private sealed class ProcessHandle(Process process) : IBackendHandle
    {
        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!process.HasExited)
                {
                    // The graceful path is /admin/shutdown; this is the backstop for a backend
                    // that ignored it.
                    if (!process.WaitForExit(3000)) process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
```

- [ ] **Step 7: Wire the supervisor into `GatewayApp.Build`**

In `McpGateway/GatewayApp.cs`, add these usings:

```csharp
using McpGateway.Supervision;
```

Replace the service registrations block (currently `AddSingleton(options)` and `AddSingleton(ManifestStore.Load(...))`) with:

```csharp
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(ManifestStore.Load(options.ManifestPath));
        builder.Services.AddSingleton<IBackendLauncher, ProcessBackendLauncher>();
        builder.Services.AddSingleton(new HealthProbe(new HttpClient()));
        builder.Services.AddSingleton(sp => new BackendSupervisor(
            sp.GetRequiredService<ManifestStore>(),
            sp.GetRequiredService<IBackendLauncher>(),
            sp.GetRequiredService<HealthProbe>(),
            options,
            token,
            sp.GetRequiredService<ILogger<BackendSupervisor>>()));
```

- [ ] **Step 8: Run the tests to verify they pass**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: 19 passed (9 from Task 3, 10 here).

- [ ] **Step 9: Verify the tests can fail**

In `GetOrStartAsync`, replace the `Lazy<Task<BackendInstance>>` with a plain start-every-time call:
temporarily change the body to `return await StartAsync(key, ResolveEntry(key.Server).ActiveVersion, cancellationToken);`.
Rerun. Expected: `ReusesTheSameBackendForTheSameKey` and `ConcurrentCallersShareOneStart` both go red. Restore it.

Then delete the `RemoveIfSame(key, entry);` line in the `catch` block and rerun. Expected: `RetriesAfterAFailedStart` goes red. Restore it.

Then in `BackendInstance.Release`, change `if (--_inFlight == 0)` to `if (--_inFlight >= 0)` and rerun. Expected: `WaitForDrainAsync_StaysPending_UntilEveryOverlappingRequestFinishes` goes red — it is the only test with two overlapping leases, and a single-lease test cannot tell `== 0` from `>= 0`. Restore it.

- [ ] **Step 10: Commit**

```bash
git add McpGateway McpGateway.Tests
git commit -m "feat: add backend supervisor with lazy start and health gate"
```

---

### Task 5: Routing and pool keying

`POST /{server}/mcp` reaches the right backend. This is where `shared` and `per-client` stop being
manifest strings and start being process isolation.

**Files:**
- Create: `McpGateway/Routing/ClientIdentity.cs`
- Create: `McpGateway/Routing/McpForwarder.cs`
- Create: `McpGateway.Tests/ClientIdentityTests.cs`
- Create: `McpGateway.Tests/RoutingTests.cs`
- Modify: `McpGateway/GatewayApp.cs`

**Interfaces:**
- Consumes: `BackendSupervisor.GetOrStartAsync`, `BackendKey`, `BackendInstance.DestinationPrefix`, `BackendInstance.BeginRequest`, `ServerEntry.IsShared`, `BackendStartupException` (Task 4).
- Produces:
  - `ClientIdentity.ResolvePoolKey(HttpContext, ServerEntry) -> string` — `""` for shared, the `X-Mcp-Client` header value (or `"default"`) for per-client
  - `McpForwarder.ForwardAsync(HttpContext, string server, string suffix) -> Task` — used by the `/{server}/mcp` and `/{server}/health` endpoints
  - Route shape `/{server}/mcp` and `/{server}/health`, which Task 13 writes into both client configs

> If Task 1's spike found a per-session discriminator, `ResolvePoolKey` reads that header instead of
> `X-Mcp-Client`, and `ClientIdentityTests` asserts on it. Everything else in this task is unchanged.

- [ ] **Step 1: Write the failing identity test**

Create `McpGateway.Tests/ClientIdentityTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run to verify it fails**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `ClientIdentity` does not exist.

- [ ] **Step 3: Implement `ClientIdentity`**

Create `McpGateway/Routing/ClientIdentity.cs`:

```csharp
using McpGateway.Configuration;
using Microsoft.AspNetCore.Http;

namespace McpGateway.Routing;

public static class ClientIdentity
{
    public const string HeaderName = "X-Mcp-Client";
    public const string Default = "default";

    /// <summary>
    /// The pool key for this request. Empty means every caller shares one backend. Otherwise the
    /// calling client gets its own — which is what reproduces the isolation stdio used to give
    /// each session for free.
    /// </summary>
    public static string ResolvePoolKey(HttpContext context, ServerEntry entry)
    {
        if (entry.IsShared) return string.Empty;

        string? raw = context.Request.Headers[HeaderName].FirstOrDefault();

        return string.IsNullOrWhiteSpace(raw) ? Default : raw.Trim().ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Write the failing routing test**

Create `McpGateway.Tests/RoutingTests.cs`. It stands a gateway up on an ephemeral port with the fake launcher swapped in, so it exercises the real endpoint and forwarder:

```csharp
using System.Net;
using System.Net.Http.Json;
using McpGateway;
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
            "deployRoot": "deploy/demo", "activeVersion": "v-one",
            "pool": "shared", "startupTimeoutSeconds": 10
          },
          "client-demo": {
            "project": "Demo/Demo.csproj", "assembly": "Demo.dll",
            "deployRoot": "deploy/demo", "activeVersion": "v-one",
            "pool": "per-client", "startupTimeoutSeconds": 10
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
    public async Task Forwarding_KeepsTheClientHeader_DropsTheGatewayToken_AndPreservesTheQuery()
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

        Assert.Contains("\"clientHeader\":\"code\"", body);
        Assert.Contains("\"authHeader\":null", body);
        Assert.Contains("trace=abc123", body);
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
```

- [ ] **Step 5: Run to verify it fails**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `GatewayApp.Build` has no two-argument overload, and the routes don't exist.

- [ ] **Step 6: Implement the forwarder**

Create `McpGateway/Routing/McpForwarder.cs`:

```csharp
using McpGateway.Configuration;
using McpGateway.Supervision;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace McpGateway.Routing;

public sealed class McpForwarder(
    IHttpForwarder forwarder,
    BackendSupervisor supervisor,
    ManifestStore manifest,
    ILogger<McpForwarder> logger)
{
    // Long timeout: a streamable-HTTP POST response can be a text/event-stream the handler holds
    // open, and YARP must not cut it short.
    private readonly HttpMessageInvoker _invoker = new(new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        ActivityHeadersPropagator = null,
        ConnectTimeout = TimeSpan.FromSeconds(15)
    });

    private static readonly ForwarderRequestConfig RequestConfig = new()
    {
        ActivityTimeout = TimeSpan.FromMinutes(10)
    };

    public async Task ForwardAsync(HttpContext context, string server, string suffix)
    {
        if (!manifest.TryGet(server, out ServerEntry? entry))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"No server named '{server}'.");
            return;
        }

        var key = new BackendKey(server, ClientIdentity.ResolvePoolKey(context, entry!));

        BackendInstance instance;
        try
        {
            instance = await supervisor.GetOrStartAsync(key, context.RequestAborted);
        }
        catch (BackendStartupException ex)
        {
            logger.LogError(ex, "Could not start {Key}", key);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync(
                ex.LogTail.Length == 0 ? ex.Message : $"{ex.Message}\n\n{ex.LogTail}");
            return;
        }

        using IDisposable lease = instance.BeginRequest();

        // Rewrite /{server}/mcp to /mcp — backends don't know they're behind a gateway.
        var transformer = new PathTransformer(suffix);

        ForwarderError error = await forwarder.SendAsync(
            context, instance.DestinationPrefix, _invoker, RequestConfig, transformer);

        if (error != ForwarderError.None)
        {
            logger.LogWarning("Forwarding to {Key} failed with {Error}", key, error);
        }
    }

    private sealed class PathTransformer(string suffix) : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(
            HttpContext context,
            HttpRequestMessage request,
            string destinationPrefix,
            CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(
                context, request, destinationPrefix, cancellationToken);

            // Keep the query string. base.TransformRequestAsync built it into RequestUri, and
            // overwriting the URI here would drop it silently rather than failing loudly.
            request.RequestUri = new Uri(
                destinationPrefix.TrimEnd('/') + suffix + context.Request.QueryString);

            // X-Mcp-Client must survive -- the backend's McpCaller.ClientId reads it. The
            // gateway's bearer token must not: it is also the backend's own shutdown token, and a
            // tool call has no reason to carry it.
            request.Headers.Remove("Authorization");

            request.Headers.Host = null;
        }
    }
}
```

Add `using System.Net;` at the top of that file for `DecompressionMethods`.

- [ ] **Step 7: Add the routes and the test seam to `GatewayApp`**

In `McpGateway/GatewayApp.cs`, add:

```csharp
using McpGateway.Routing;
using Yarp.ReverseProxy.Forwarder;
```

Change the signature of `Build` so tests can substitute the launcher, and add the routes:

```csharp
    public static WebApplication Build(
        GatewayBuildOptions options,
        Action<IServiceCollection>? configureServices = null)
    {
```

After the existing `AddSingleton` calls and before `builder.Build()`, add:

```csharp
        builder.Services.AddHttpForwarder();
        builder.Services.AddSingleton<McpForwarder>();

        configureServices?.Invoke(builder.Services);
```

`configureServices` runs last so a test registration replaces the default `IBackendLauncher`.

After the `/admin/servers` mapping, add:

```csharp
        app.MapPost("/{server}/mcp", (HttpContext ctx, McpForwarder fwd, string server) =>
            fwd.ForwardAsync(ctx, server, "/mcp"));

        app.MapGet("/{server}/health", (HttpContext ctx, McpForwarder fwd, string server) =>
            fwd.ForwardAsync(ctx, server, "/health"));
```

- [ ] **Step 8: Run the tests to verify they pass**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: 35 passed (24 from Tasks 3-4, 11 here). Task 4's fix rounds added 5 tests beyond its original 10.

- [ ] **Step 9: Verify the tests can fail**

In `ClientIdentity.ResolvePoolKey`, delete the `if (entry.IsShared) return string.Empty;` line and rerun. Expected: `SharedServers_IgnoreTheClientHeader` and `SharedServer_ServesEveryClientFromOneBackend` both go red. Restore it.

Then make it always return `Default` (delete the header read) and rerun. Expected: `PerClientServer_GivesEachClientItsOwnBackend` goes red — this is the test that proves per-client isolation is real, so confirm it specifically. Restore it.

Then in `McpForwarder.ForwardAsync`, delete the `catch (BackendStartupException ...)` block and rerun. Expected: `FailedStart_Is503WithDetail` goes red. Restore it.

- [ ] **Step 10: Commit**

```bash
git add McpGateway McpGateway.Tests
git commit -m "feat: route /{server}/mcp to pooled backends by client identity"
```

---

### Task 6: Idle stop and crash recovery

Fifteen permanently-resident services would be a worse resource story than the stdio processes they
replace. Idle stop is what makes lazy start pay.

**Files:**
- Create: `McpGateway/Supervision/IdleReaper.cs`
- Create: `McpGateway/Supervision/EagerStarter.cs`
- Create: `McpGateway.Tests/IdleReaperTests.cs`
- Modify: `McpGateway/Supervision/BackendSupervisor.cs`
- Modify: `McpGateway/GatewayApp.cs`

**Interfaces:**
- Consumes: `BackendSupervisor.All`, `.StopAsync`, `.ResolveEntry`, `BackendInstance.LastUsedAt`, `.InFlight`, `ServerEntry.IdleTimeoutMinutes` (Tasks 3–4).
- Produces:
  - `IdleReaper(BackendSupervisor, TimeProvider, ILogger<IdleReaper>) : BackgroundService`
  - `IdleReaper.SweepAsync(CancellationToken) -> Task<int>` — returns how many backends it stopped; called directly by tests rather than waiting on a timer
  - `BackendSupervisor.EvictExitedAsync(CancellationToken) -> Task<int>`
  - `EagerStarter(BackendSupervisor, ManifestStore, ILogger<EagerStarter>) : IHostedService` with `StartEagerServersAsync(CancellationToken) -> Task`

- [ ] **Step 1: Write the failing test**

Create `McpGateway.Tests/IdleReaperTests.cs`:

```csharp
using McpGateway;
using McpGateway.Configuration;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace McpGateway.Tests;

public sealed class IdleReaperTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-reaper-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-08-30T12:00:00Z"));
    private readonly BackendSupervisor _supervisor;
    private readonly IdleReaper _reaper;

    public IdleReaperTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "reaps": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "per-client",
            "idleTimeoutMinutes": 30, "startupTimeoutSeconds": 10
          },
          "never-reaps": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "shared",
            "eagerStart": true,
            "idleTimeoutMinutes": 0, "startupTimeoutSeconds": 10
          },
          "shared-lazy": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "shared",
            "eagerStart": false,
            "idleTimeoutMinutes": 30, "startupTimeoutSeconds": 10
          }
        }
        """);

        _supervisor = new BackendSupervisor(
            ManifestStore.Load(manifestPath), _launcher, new HealthProbe(new HttpClient()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                RepoRoot = _root
            },
            "shutdown-token", NullLogger<BackendSupervisor>.Instance);

        _reaper = new IdleReaper(_supervisor, _time, NullLogger<IdleReaper>.Instance);
    }

    [Fact]
    public async Task Sweep_StopsABackendIdleLongerThanItsTimeout()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(1, await _reaper.SweepAsync(TestContext.Current.CancellationToken));
        Assert.False(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task Sweep_LeavesABackendInsideItsTimeout()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromMinutes(29));

        Assert.Equal(0, await _reaper.SweepAsync(TestContext.Current.CancellationToken));
        Assert.True(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task Sweep_NeverStopsAServerWithTimeoutZero()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("never-reaps", ""), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromDays(7));

        Assert.Equal(0, await _reaper.SweepAsync(TestContext.Current.CancellationToken));
        Assert.True(_supervisor.TryGet(new BackendKey("never-reaps", ""), out _));
    }

    [Fact]
    public async Task Sweep_LeavesABackendWithRequestsInFlight()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        using IDisposable lease = instance.BeginRequest();
        _time.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(0, await _reaper.SweepAsync(TestContext.Current.CancellationToken));

        // Assert the backend survived, not just the count: a sweep that stopped it and still
        // returned 0 would otherwise slip through.
        Assert.True(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task EvictExitedAsync_DropsACrashedBackend()
    {
        BackendInstance instance = await _supervisor.GetOrStartAsync(
            new BackendKey("reaps", "code"), TestContext.Current.CancellationToken);

        await instance.Handle.DisposeAsync();

        Assert.Equal(1, await _supervisor.EvictExitedAsync(TestContext.Current.CancellationToken));
        Assert.False(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task GetOrStartAsync_RestartsAfterACrash()
    {
        var key = new BackendKey("reaps", "code");

        BackendInstance first = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);
        await first.Handle.DisposeAsync();

        BackendInstance second = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.NotSame(first, second);
        Assert.Equal(2, _launcher.StartCount);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

Add the fake time provider package. In `Directory.Packages.props`:

```xml
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="9.10.0" />
```

In `McpGateway.Tests/McpGateway.Tests.csproj`:

```xml
        <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
```

- [ ] **Step 2: Run to verify it fails**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `IdleReaper` and `BackendSupervisor.EvictExitedAsync` do not exist.

- [ ] **Step 3: Add `EvictExitedAsync` to the supervisor**

In `McpGateway/Supervision/BackendSupervisor.cs`, add:

```csharp
    /// <summary>Drops backends whose process has gone. The next request starts a fresh one.</summary>
    public async Task<int> EvictExitedAsync(CancellationToken cancellationToken)
    {
        var evicted = 0;

        foreach (BackendInstance instance in All)
        {
            if (!instance.Handle.HasExited) continue;

            logger.LogWarning(
                "{Key} exited unexpectedly (pid {Pid}); it will restart on the next request",
                instance.Key, instance.Handle.ProcessId);

            await StopAsync(instance.Key, cancellationToken);
            evicted++;
        }

        return evicted;
    }
```

- [ ] **Step 4: Implement the reaper**

Create `McpGateway/Supervision/IdleReaper.cs`:

```csharp
using McpGateway.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

/// <summary>
/// Stops backends nobody has used lately. Without this, moving fifteen servers to long-lived
/// services would cost more memory than the per-session stdio processes it replaces.
/// </summary>
public sealed class IdleReaper(
    BackendSupervisor supervisor,
    TimeProvider time,
    ILogger<IdleReaper> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await supervisor.EvictExitedAsync(cancellationToken);

        var stopped = 0;
        DateTimeOffset now = time.GetUtcNow();

        foreach (BackendInstance instance in supervisor.All)
        {
            ServerEntry entry = supervisor.ResolveEntry(instance.Key.Server);

            // Zero means never reap — used for eagerly started servers like code-assist whose
            // startup cost is a graph build.
            if (entry.IdleTimeoutMinutes <= 0) continue;
            if (instance.InFlight > 0) continue;

            if (now - instance.LastUsedAt < TimeSpan.FromMinutes(entry.IdleTimeoutMinutes)) continue;

            logger.LogInformation(
                "Stopping {Key}; idle since {LastUsed}", instance.Key, instance.LastUsedAt);

            await supervisor.StopAsync(instance.Key, cancellationToken);
            stopped++;
        }

        return stopped;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SweepInterval, time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Idle sweep failed");
            }
        }
    }
}
```

`BackendInstance.LastUsedAt` uses `DateTimeOffset.UtcNow` directly, so the reaper's clock and the
instance's clock would disagree under a fake provider. Thread one `TimeProvider` through both.

In `McpGateway/Supervision/BackendInstance.cs`, add a trailing parameter to the primary constructor:

```csharp
public sealed class BackendInstance(
    BackendKey key,
    string version,
    int port,
    IBackendHandle handle,
    string shutdownToken,
    TimeProvider time)
```

Replace both `DateTimeOffset.UtcNow` uses in that file with `time.GetUtcNow()` — the `LastUsedAt`
initialiser and the two assignments in `BeginRequest` and `Release`.

In `McpGateway/Supervision/BackendSupervisor.cs`, add a trailing **optional** parameter to its
primary constructor so the six-argument call sites in Tasks 7 and 8 keep compiling:

```csharp
public sealed class BackendSupervisor(
    ManifestStore manifest,
    IBackendLauncher launcher,
    HealthProbe healthProbe,
    GatewayBuildOptions options,
    string shutdownToken,
    ILogger<BackendSupervisor> logger,
    TimeProvider? time = null) : IAsyncDisposable
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
```

and pass `_time` as the last argument to the `new BackendInstance(...)` call at the end of
`StartAsync`.

In `IdleReaperTests`, pass the fake as that seventh argument — change the constructor call to end:

```csharp
            "shutdown-token", NullLogger<BackendSupervisor>.Instance, _time);
```

In `GatewayApp.cs`, add `sp.GetRequiredService<TimeProvider>()` as the final argument of the
`BackendSupervisor` factory registered in Task 4.

- [ ] **Step 5: Register the reaper**

In `McpGateway/GatewayApp.cs`, before `configureServices?.Invoke(...)`:

```csharp
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IdleReaper>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<IdleReaper>());
```

- [ ] **Step 6: Run the tests to verify they pass**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: 41 passed (35 from Tasks 3-5, 6 here).

- [ ] **Step 7: Verify the tests can fail**

Delete the `if (entry.IdleTimeoutMinutes <= 0) continue;` guard and rerun. Expected: `Sweep_NeverStopsAServerWithTimeoutZero` goes red. Restore it.

Delete `if (instance.InFlight > 0) continue;` and rerun. Expected: `Sweep_LeavesABackendWithRequestsInFlight` goes red. Restore it.

In `GetOrStartAsync`, delete the `if (instance.Handle.HasExited) { RemoveIfSame(...); continue; }` block and rerun. Expected: `GetOrStartAsync_RestartsAfterACrash` goes red. Restore it.

- [ ] **Step 8: Honour `eagerStart`**

`eagerStart` is read from the manifest and reported by `/admin/servers`, but so far nothing acts on
it. It is the spec's escape hatch for the risk that lazy-start latency exceeds the client's request
timeout — CodeAssist's first call would otherwise pay for a graph build.

Write the failing test first. Add to `McpGateway.Tests/IdleReaperTests.cs`:

```csharp
    [Fact]
    public async Task EagerStarter_StartsOnlyTheServersMarkedEager()
    {
        var starter = new EagerStarter(
            _supervisor,
            ManifestStore.Load(Path.Combine(_root, "servers.json")),
            NullLogger<EagerStarter>.Instance);

        await starter.StartEagerServersAsync(TestContext.Current.CancellationToken);

        // "shared-lazy" is the load-bearing assertion: it is shared, so EagerStarter's IsShared
        // branch does not filter it, which leaves the EagerStart guard as the only thing keeping
        // it from starting. A per-client server would be filtered either way and prove nothing.
        Assert.True(_supervisor.TryGet(new BackendKey("never-reaps", ""), out _));
        Assert.False(_supervisor.TryGet(new BackendKey("shared-lazy", ""), out _));
        Assert.False(_supervisor.TryGet(new BackendKey("reaps", "code"), out _));
    }

    [Fact]
    public async Task EagerStarter_DoesNotThrow_WhenAnEagerServerFailsToStart()
    {
        _launcher.SuppressPortFile = true;

        var starter = new EagerStarter(
            _supervisor,
            ManifestStore.Load(Path.Combine(_root, "servers.json")),
            NullLogger<EagerStarter>.Instance);

        // A backend that won't come up must not take the gateway down with it.
        await starter.StartEagerServersAsync(TestContext.Current.CancellationToken);

        Assert.False(_supervisor.TryGet(new BackendKey("never-reaps", ""), out _));
    }
```

The manifest above already marks `never-reaps` eager and adds `shared-lazy` as a shared but
non-eager control. Both are needed: without the control, the guard cannot be isolated.

Run and confirm it fails to compile: `EagerStarter` does not exist.

Create `McpGateway/Supervision/EagerStarter.cs`:

```csharp
using McpGateway.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

/// <summary>
/// Starts the servers whose first-call latency would otherwise be paid by a user request. Only
/// servers marked eagerStart — everything else stays lazy, which is what keeps fifteen HTTP
/// services cheaper than fifteen stdio processes.
/// </summary>
public sealed class EagerStarter(
    BackendSupervisor supervisor,
    ManifestStore manifest,
    ILogger<EagerStarter> logger) : IHostedService
{
    public async Task StartEagerServersAsync(CancellationToken cancellationToken)
    {
        foreach ((string name, ServerEntry entry) in manifest.Entries)
        {
            if (!entry.EagerStart) continue;

            // Shared servers have one backend with an empty pool key. A per-client server has no
            // client to start for yet, so eager start only makes sense for shared ones.
            if (!entry.IsShared)
            {
                logger.LogWarning(
                    "{Server} is marked eagerStart but pooled per-client; skipping", name);
                continue;
            }

            try
            {
                await supervisor.GetOrStartAsync(new BackendKey(name, string.Empty), cancellationToken);
                logger.LogInformation("Eagerly started {Server}", name);
            }
            catch (Exception ex)
            {
                // The gateway must come up even if one backend cannot.
                logger.LogError(ex, "Could not eagerly start {Server}", name);
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fire and forget: the gateway must accept requests immediately, and anything not yet
        // started simply starts lazily on its first call.
        _ = Task.Run(() => StartEagerServersAsync(CancellationToken.None), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Register it in `McpGateway/GatewayApp.cs`, beside the `IdleReaper` registration:

```csharp
        builder.Services.AddSingleton<EagerStarter>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<EagerStarter>());
```

Run the tests. Expected: 43 passed.

Verify they can fail: delete the `if (!entry.EagerStart) continue;` line and rerun. Expected:
`EagerStarter_StartsOnlyTheServersMarkedEager` goes red on the `shared-lazy` assertion, because
that server is shared and only the guard was keeping it from starting. Restore it.
Then delete the `try`/`catch` around `GetOrStartAsync` and rerun. Expected:
`EagerStarter_DoesNotThrow_WhenAnEagerServerFailsToStart` goes red. Restore it.

- [ ] **Step 9: Commit**

```bash
git add McpGateway McpGateway.Tests Directory.Packages.props
git commit -m "feat: stop idle backends, recover from crashed ones, start eager ones"
```

---

### Task 7: Blue/green activation

The upgrade path for servers that tolerate two live instances. Start the new version, health-gate
it, flip the route, drain the old one, stop it. No failed calls.

**Files:**
- Create: `McpGateway/Upgrade/ActivationService.cs`
- Create: `McpGateway/Upgrade/ActivationResult.cs`
- Create: `McpGateway.Tests/BlueGreenActivationTests.cs`
- Modify: `McpGateway/GatewayApp.cs`

**Interfaces:**
- Consumes: `BackendSupervisor.StartDetachedAsync`, `.Replace`, `.TryGet`, `.All`, `.ResolveEntry`; `BackendInstance.WaitForDrainAsync`, `.StopAsync`, `.Version`; `ManifestStore.SetActiveVersionAsync`; `ServerEntry.OverlapAllowed` (Tasks 3–4).
- Produces:
  - `ActivationResult` — `record(bool Succeeded, string Server, string FromVersion, string ToVersion, int BackendsSwapped, bool DrainTimedOut, string? Error)`
  - `ActivationService.ActivateAsync(string server, string version, CancellationToken) -> Task<ActivationResult>`
  - `ActivationService.DrainTimeout` — `TimeSpan`, default 30 seconds
  - Task 8 adds the non-overlap branch inside `ActivateAsync`; Task 9's script calls the endpoint that wraps it.

- [ ] **Step 1: Write the failing test**

Create `McpGateway.Tests/BlueGreenActivationTests.cs`:

```csharp
using McpGateway;
using McpGateway.Configuration;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class BlueGreenActivationTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-activate-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly ManifestStore _manifest;
    private readonly BackendSupervisor _supervisor;
    private readonly ActivationService _activation;

    public BlueGreenActivationTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "overlaps": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "per-client",
            "overlapAllowed": true, "startupTimeoutSeconds": 10
          }
        }
        """);

        _manifest = ManifestStore.Load(manifestPath);
        _supervisor = new BackendSupervisor(
            _manifest, _launcher, new HealthProbe(new HttpClient()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                RepoRoot = _root
            },
            "shutdown-token", NullLogger<BackendSupervisor>.Instance);

        _activation = new ActivationService(
            _supervisor, _manifest, NullLogger<ActivationService>.Instance);
    }

    [Fact]
    public async Task Activate_SwapsARunningBackendToTheNewVersion()
    {
        var key = new BackendKey("overlaps", "code");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, result.BackendsSwapped);

        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Equal("v-two", after!.Version);
        Assert.NotSame(before, after);
        Assert.True(before.Handle.HasExited);

        // The only test that asserts the manifest write alongside a real nonzero swap.
        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-two", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_SwapsEveryLiveBackendOfThatServer()
    {
        await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "code"), TestContext.Current.CancellationToken);
        await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "desktop"), TestContext.Current.CancellationToken);

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.Equal(2, result.BackendsSwapped);
    }

    [Fact]
    public async Task Activate_PersistsTheNewActiveVersion()
    {
        await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-two", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_WithNoLiveBackend_JustRecordsTheVersion()
    {
        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.BackendsSwapped);
        Assert.Equal(0, _launcher.StartCount);
    }

    [Fact]
    public async Task Activate_LeavesTheOldBackendServing_WhenTheNewOneIsUnhealthy()
    {
        var key = new BackendKey("overlaps", "code");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        _launcher.Unhealthy = true;

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);

        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Same(before, after);
        Assert.False(before.Handle.HasExited);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_SwapsNothing_WhenAnyBackendFailsToStart()
    {
        BackendInstance beforeCode = await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "code"), TestContext.Current.CancellationToken);
        BackendInstance beforeDesktop = await _supervisor.GetOrStartAsync(
            new BackendKey("overlaps", "desktop"), TestContext.Current.CancellationToken);

        // Two backends are live, so StartCount is 2. Fail the FOURTH start: the first replacement
        // comes up healthy and the second does not, which is the only shape that distinguishes
        // all-or-nothing from swap-as-you-go. A global Unhealthy flag would fail the first
        // replacement too, and both implementations would then look identical.
        _launcher.UnhealthyFromStartNumber = 4;

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.BackendsSwapped);

        // Neither backend may have moved, and the manifest must still agree with them.
        Assert.True(_supervisor.TryGet(new BackendKey("overlaps", "code"), out BackendInstance? code));
        Assert.Same(beforeCode, code);
        Assert.False(beforeCode.Handle.HasExited);

        Assert.True(_supervisor.TryGet(new BackendKey("overlaps", "desktop"), out BackendInstance? desktop));
        Assert.Same(beforeDesktop, desktop);
        Assert.False(beforeDesktop.Handle.HasExited);

        // The healthy first replacement must have been stopped by the cleanup loop, not left
        // running. Index 2 is the first replacement: starts 0 and 1 are the two live backends.
        Assert.True(_launcher.Handles[2].HasExited);

        Assert.True(_manifest.TryGet("overlaps", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    [Fact]
    public async Task Activate_ReportsADrainTimeout_ButStillSwaps()
    {
        var key = new BackendKey("overlaps", "code");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        // A request that never finishes is the one window where an upgrade can cost a call.
        using IDisposable stuck = before.BeginRequest();

        _activation.DrainTimeout = TimeSpan.FromMilliseconds(200);

        ActivationResult result = await _activation.ActivateAsync(
            "overlaps", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.DrainTimedOut);
        Assert.True(_supervisor.TryGet(key, out BackendInstance? after));
        Assert.Equal("v-two", after!.Version);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `ActivationService` and `ActivationResult` do not exist.

- [ ] **Step 3: Implement the result type**

Create `McpGateway/Upgrade/ActivationResult.cs`:

```csharp
namespace McpGateway.Upgrade;

public sealed record ActivationResult(
    bool Succeeded,
    string Server,
    string FromVersion,
    string ToVersion,
    int BackendsSwapped,
    bool DrainTimedOut,
    string? Error);
```

- [ ] **Step 4: Implement the service**

Create `McpGateway/Upgrade/ActivationService.cs`:

```csharp
using McpGateway.Configuration;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging;

namespace McpGateway.Upgrade;

public sealed class ActivationService(
    BackendSupervisor supervisor,
    ManifestStore manifest,
    ILogger<ActivationService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// How long in-flight requests get to finish before the old backend is killed anyway. This is
    /// the only window in which an upgrade can fail a call.
    /// </summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<ActivationResult> ActivateAsync(
        string server, string version, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ServerEntry entry = supervisor.ResolveEntry(server);
            string from = entry.ActiveVersion;

            List<BackendInstance> live = supervisor.All
                .Where(instance => instance.Key.Server == server)
                .ToList();

            // Start and health-gate EVERY replacement before touching a single live backend.
            // Swapping as we go would leave earlier backends on the new version with their old
            // instances already stopped, while a later failure skipped the manifest write -- a
            // fleet running one version and a manifest claiming another, which everything
            // downstream reads.
            var replacements = new List<(BackendInstance Old, BackendInstance New)>();

            try
            {
                foreach (BackendInstance old in live)
                {
                    replacements.Add(
                        (old, await supervisor.StartDetachedAsync(old.Key, version, cancellationToken)));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex, "Could not start {Server} at {Version}; nothing was swapped", server, version);

                foreach ((_, BackendInstance started) in replacements)
                {
                    await started.StopAsync(cancellationToken);
                }

                return new ActivationResult(
                    false, server, from, version, 0, false,
                    $"New version failed to start: {ex.Message}");
            }

            var drainTimedOut = false;

            foreach ((BackendInstance old, BackendInstance replacement) in replacements)
            {
                supervisor.Replace(old.Key, replacement);

                if (!await old.WaitForDrainAsync(DrainTimeout, cancellationToken))
                {
                    logger.LogWarning(
                        "{Key} still had {InFlight} request(s) after {Timeout}; stopping anyway",
                        old.Key, old.InFlight, DrainTimeout);
                    drainTimedOut = true;
                }

                await old.StopAsync(cancellationToken);
            }

            int swapped = replacements.Count;

            await manifest.SetActiveVersionAsync(server, version, cancellationToken);

            logger.LogInformation(
                "Activated {Server} {From} -> {To}, {Count} backend(s) swapped",
                server, from, version, swapped);

            return new ActivationResult(
                true, server, from, version, swapped, drainTimedOut, null);
        }
        finally
        {
            _gate.Release();
        }
    }
}
```

- [ ] **Step 5: Register it**

In `McpGateway/GatewayApp.cs`, add `using McpGateway.Upgrade;` and, before `configureServices?.Invoke(...)`:

```csharp
        builder.Services.AddSingleton<ActivationService>();
```

- [ ] **Step 6: Run the tests to verify they pass**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: all green, with 6 more tests than before this task. Do not treat a cumulative total as authoritative — earlier fix rounds add tests, so only the delta is stable.

- [ ] **Step 7: Verify the tests can fail**

Move the `await manifest.SetActiveVersionAsync(...)` call to the top of the method, before the loop, and rerun. Expected: `LeavesTheOldBackendServing_WhenTheNewOneIsUnhealthy` goes red on its `activeVersion` assertion — which is the point of putting the write last. Restore it.

Then change the `catch` block to `supervisor.Replace(old.Key, replacement);` regardless of failure — i.e. move `Replace` above the `try`. Rerun. Expected: the same test goes red on `Assert.Same(before, after)`. Restore it.

Then set `DrainTimeout` handling to ignore the return value (`await old.WaitForDrainAsync(...);` with no `if`) and rerun. Expected: `ReportsADrainTimeout_ButStillSwaps` goes red. Restore it.

Then revert the two-phase structure to a single swap-as-you-go loop with an early return on a failed
start, and rerun. Expected: `Activate_SwapsNothing_WhenAnyBackendFailsToStart` goes red on one of
the `Assert.Same` or `HasExited` assertions, because whichever backend was swapped first is already
gone by the time the second start fails. Note the order of `supervisor.All` is not guaranteed, so
which of the two assertions fails is not fixed — the test asserts both are untouched, so either
failure is the right signal. Restore it.

- [ ] **Step 8: Commit**

```bash
git add McpGateway McpGateway.Tests
git commit -m "feat: blue/green activation with drain and rollback on failed start"
```

---

### Task 8: Hold-and-swap activation, and the admin API

> **Superseded in places — read this first.** Task 8 took three fix rounds and the code below is the
> pre-fix version. The shipped implementation differs in four ways, all in commits
> `0ff77f6..c139836` and reasoned through in the SDD ledger:
> 1. `FakeBackendLauncher`'s `_live` increment is unconditional (only the peak recording is gated).
>    Gating both inverts `Activate_NeverRunsTwoInstancesAtOnce` so correct code fails and the
>    overlapping path passes. This is corrected in Step 1 below.
> 2. The swap phase of **both** activation paths is wrapped so an exception from `WaitForDrainAsync`,
>    `StopAsync` or `SetActiveVersionAsync` returns a failed `ActivationResult` rather than escaping
>    as a 500, and the non-overlap restore reports distinctly whether the old version came back.
> 3. `ActivateAsync` takes `string? version`, resolving null to the active version **inside** the
>    gate. `/restart` passes null; reading it outside lets a queued restart revert a concurrent
>    deploy. `/admin/prune` goes through a gated `ActivationService.PruneAsync`.
> 4. `PruneVersionsAsync` and `/restart` gained the tests this task shipped without.
>
> Treat git as authoritative for this task. The steps below are still the right order of work.

CodeAssist writes to a machine-wide `%LocalAppData%\CodeAssist\indexes`, so two live instances would
corrupt it. Its upgrade path drains, stops, starts the new version, and holds arriving requests in
the meantime rather than refusing them. Latency instead of errors.

**Files:**
- Create: `McpGateway/Endpoints/AdminEndpoints.cs`
- Create: `McpGateway.Tests/HoldAndSwapActivationTests.cs`
- Create: `McpGateway.Tests/AdminEndpointTests.cs`
- Modify: `McpGateway/Supervision/BackendSupervisor.cs`
- Modify: `McpGateway/Upgrade/ActivationService.cs`
- Modify: `McpGateway/GatewayApp.cs`

**Interfaces:**
- Consumes: everything from Tasks 3–7.
- Produces:
  - `BackendSupervisor.HoldAsync(string server, CancellationToken) -> Task<IAsyncDisposable>` — while held, `GetOrStartAsync` for that server waits
  - `BackendSupervisor.PruneVersionsAsync(string server, CancellationToken) -> Task<IReadOnlyList<string>>` — deletes version directories that are neither active nor in use
  - `ActivationService.ActivateAsync` gains the `OverlapAllowed == false` branch
  - Endpoints Task 9's scripts call: `POST /admin/servers/{name}/activate`, `POST /admin/servers/{name}/restart`, `POST /admin/servers/{name}/stop`, `POST /admin/prune`, and an extended `GET /admin/servers`

- [ ] **Step 1: Write the failing hold-and-swap test**

Create `McpGateway.Tests/HoldAndSwapActivationTests.cs`:

```csharp
using McpGateway;
using McpGateway.Configuration;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpGateway.Tests;

public sealed class HoldAndSwapActivationTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcp-holdswap-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBackendLauncher _launcher = new();
    private readonly ManifestStore _manifest;
    private readonly BackendSupervisor _supervisor;
    private readonly ActivationService _activation;

    public HoldAndSwapActivationTests()
    {
        Directory.CreateDirectory(_root);

        string manifestPath = Path.Combine(_root, "servers.json");
        File.WriteAllText(manifestPath, """
        {
          "exclusive": {
            "project": "D/D.csproj", "assembly": "D.dll", "deployRoot": "deploy/d",
            "activeVersion": "v-one", "pool": "shared",
            "overlapAllowed": false, "startupTimeoutSeconds": 10
          }
        }
        """);

        _manifest = ManifestStore.Load(manifestPath);
        _supervisor = new BackendSupervisor(
            _manifest, _launcher, new HealthProbe(new HttpClient()),
            new GatewayBuildOptions
            {
                ManifestPath = manifestPath,
                TokenPath = Path.Combine(_root, "token"),
                RepoRoot = _root
            },
            "shutdown-token", NullLogger<BackendSupervisor>.Instance);

        _activation = new ActivationService(
            _supervisor, _manifest, NullLogger<ActivationService>.Instance);
    }

    [Fact]
    public async Task Activate_NeverRunsTwoInstancesAtOnce()
    {
        var key = new BackendKey("exclusive", "");
        BackendInstance before = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        _launcher.ObserveConcurrency = true;

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(before.Handle.HasExited);
        Assert.Equal(1, _launcher.MaxConcurrentLive);
    }

    [Fact]
    public async Task Activate_HoldsArrivingRequestsRatherThanRefusingThem()
    {
        var key = new BackendKey("exclusive", "");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        _launcher.StartDelay = TimeSpan.FromMilliseconds(400);

        Task<ActivationResult> activating = _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        await Task.Delay(150, TestContext.Current.CancellationToken);

        // Arrives mid-swap. Must wait for the new backend, not fail.
        BackendInstance served = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.True((await activating).Succeeded);
        Assert.Equal("v-two", served.Version);
    }

    [Fact]
    public async Task Activate_RestartsThePreviousVersion_WhenTheNewOneFailsToStart()
    {
        var key = new BackendKey("exclusive", "");
        await _supervisor.GetOrStartAsync(key, TestContext.Current.CancellationToken);

        _launcher.Unhealthy = true;

        ActivationResult result = await _activation.ActivateAsync(
            "exclusive", "v-two", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);

        // No old process to fall back to, so the gateway brings v-one back up.
        _launcher.Unhealthy = false;
        BackendInstance recovered = await _supervisor.GetOrStartAsync(
            key, TestContext.Current.CancellationToken);

        Assert.Equal("v-one", recovered.Version);
        Assert.True(_manifest.TryGet("exclusive", out ServerEntry? entry));
        Assert.Equal("v-one", entry!.ActiveVersion);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

Extend `McpGateway.Tests/FakeBackendLauncher.cs` with the three knobs those tests use. Add these fields and update `Start`:

```csharp
    /// <summary>Track how many fakes are alive at once, to prove non-overlap.</summary>
    public bool ObserveConcurrency { get; set; }
    public int MaxConcurrentLive { get; private set; }

    private int _live;
```

`StartDelay` already exists from Task 4. At the top of `Start`, after `StartCount++;`:

```csharp
        // Paired unconditionally with FakeHandle's onExit decrement below: a handle started before
        // ObserveConcurrency was turned on (e.g. the pre-existing backend in a swap test) still
        // decrements _live when it exits, so the increment must not be gated on the flag or _live
        // goes negative and MaxConcurrentLive under-reports. Only the *recorded peak* is gated, so
        // unrelated tests that never touch ObserveConcurrency see MaxConcurrentLive stay at 0.
        int live = Interlocked.Increment(ref _live);
        if (ObserveConcurrency)
        {
            MaxConcurrentLive = Math.Max(MaxConcurrentLive, live);
        }
```

Gating the increment as well as the peak inverts the test: a *correct* non-overlap swap reports a
peak of 0 and fails, while the *incorrect* overlapping path passes. Keep the increment
unconditional.

And in `FakeHandle.DisposeAsync`, before `await app.StopAsync();`, add a callback so the count drops. Change `FakeHandle`'s constructor to `FakeHandle(WebApplication app, int pid, Action onExit)`, call `onExit()` first in `DisposeAsync`, and pass `() => Interlocked.Decrement(ref _live)` from `Start`.

- [ ] **Step 2: Run to verify it fails**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL — `HoldAsync` does not exist and the non-overlap branch is missing.

- [ ] **Step 3: Add holds to the supervisor**

In `McpGateway/Supervision/BackendSupervisor.cs`, add the field:

```csharp
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _holds =
        new(StringComparer.OrdinalIgnoreCase);
```

Add the method:

```csharp
    /// <summary>
    /// Blocks new starts for a server while a swap is in progress. Callers of GetOrStartAsync wait
    /// on the hold instead of getting an error, which is what makes a stop-then-start upgrade cost
    /// latency rather than failed calls.
    /// </summary>
    public Task<IAsyncDisposable> HoldAsync(string server, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_holds.TryAdd(server, source))
        {
            throw new InvalidOperationException($"A swap is already in progress for '{server}'.");
        }

        return Task.FromResult<IAsyncDisposable>(new Hold(this, server, source));
    }

    private sealed class Hold(BackendSupervisor owner, string server, TaskCompletionSource source)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner._holds.TryRemove(server, out _);
            source.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
```

At the very start of the `while (true)` body in `GetOrStartAsync`, before `_pool.GetOrAdd`:

```csharp
            if (_holds.TryGetValue(key.Server, out TaskCompletionSource? hold))
            {
                await hold.Task.WaitAsync(cancellationToken);
            }
```

- [ ] **Step 4: Add the non-overlap branch to `ActivationService`**

In `ActivateAsync`, immediately after `List<BackendInstance> live = ...`, insert:

```csharp
            if (!entry.OverlapAllowed)
            {
                return await ActivateExclusiveAsync(
                    server, from, version, live, cancellationToken);
            }
```

Then add the method:

```csharp
    /// <summary>
    /// For servers whose machine-wide state two live instances would corrupt. Requests arriving
    /// mid-swap are held by the supervisor until the new backend is up.
    /// </summary>
    private async Task<ActivationResult> ActivateExclusiveAsync(
        string server,
        string from,
        string version,
        List<BackendInstance> live,
        CancellationToken cancellationToken)
    {
        await using IAsyncDisposable hold = await supervisor.HoldAsync(server, cancellationToken);

        var drainTimedOut = false;
        var swapped = 0;

        foreach (BackendInstance old in live)
        {
            if (!await old.WaitForDrainAsync(DrainTimeout, cancellationToken)) drainTimedOut = true;

            await supervisor.StopAsync(old.Key, cancellationToken);

            try
            {
                BackendInstance replacement = await supervisor.StartDetachedAsync(
                    old.Key, version, cancellationToken);

                supervisor.Replace(old.Key, replacement);
                swapped++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not start {Key} at {Version}; restoring {From}",
                    old.Key, version, from);

                // Nothing to fall back to, so bring the previous version back up. Held requests
                // survive if this succeeds.
                try
                {
                    BackendInstance restored = await supervisor.StartDetachedAsync(
                        old.Key, from, cancellationToken);
                    supervisor.Replace(old.Key, restored);
                }
                catch (Exception restoreFailure)
                {
                    logger.LogCritical(restoreFailure,
                        "Could not restore {Key} at {From}; it will start on the next request",
                        old.Key, from);
                }

                return new ActivationResult(
                    false, server, from, version, swapped, drainTimedOut,
                    $"New version failed to start: {ex.Message}");
            }
        }

        await manifest.SetActiveVersionAsync(server, version, cancellationToken);

        return new ActivationResult(true, server, from, version, swapped, drainTimedOut, null);
    }
```

- [ ] **Step 5: Run the hold-and-swap tests**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: all green, with 3 more tests than before this step.

- [ ] **Step 6: Verify those tests can fail**

Delete the `if (!entry.OverlapAllowed) { return await ActivateExclusiveAsync(...); }` branch so the exclusive server takes the blue/green path, and rerun. Expected: `Activate_NeverRunsTwoInstancesAtOnce` goes red on `MaxConcurrentLive`. Restore it.

Delete the hold-await block from `GetOrStartAsync` and rerun. Expected: `Activate_HoldsArrivingRequestsRatherThanRefusingThem` goes red — the mid-swap caller starts its own backend at the old version. Restore it.

Delete the inner restore `try` in `ActivateExclusiveAsync` and rerun. Expected: `RestartsThePreviousVersion_WhenTheNewOneFailsToStart` goes red. Restore it.

- [ ] **Step 7: Implement version pruning**

In `McpGateway/Supervision/BackendSupervisor.cs`, add:

```csharp
    /// <summary>
    /// Deletes version directories that are neither active nor backing a live backend. A directory
    /// whose files are still locked is skipped rather than fought.
    /// </summary>
    public Task<IReadOnlyList<string>> PruneVersionsAsync(
        string server, CancellationToken cancellationToken)
    {
        ServerEntry entry = ResolveEntry(server);
        string root = Path.Combine(options.RepoRoot, entry.DeployRoot);

        var pruned = new List<string>();
        if (!Directory.Exists(root)) return Task.FromResult<IReadOnlyList<string>>(pruned);

        HashSet<string> keep = All
            .Where(instance => instance.Key.Server == server)
            .Select(instance => instance.Version)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        keep.Add(entry.ActiveVersion);

        foreach (string directory in Directory.GetDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name = Path.GetFileName(directory);
            if (keep.Contains(name)) continue;

            try
            {
                Directory.Delete(directory, recursive: true);
                pruned.Add(name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogInformation(
                    "Left {Directory} in place; something still holds it", directory);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(pruned);
    }
```

- [ ] **Step 8: Write the failing admin endpoint test**

Create `McpGateway.Tests/AdminEndpointTests.cs`:

```csharp
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
```

- [ ] **Step 9: Implement the admin endpoints**

Create `McpGateway/Endpoints/AdminEndpoints.cs`:

```csharp
using McpGateway.Configuration;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace McpGateway.Endpoints;

public sealed record ActivateRequest(string Version);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/servers", (ManifestStore manifest, BackendSupervisor supervisor) =>
        {
            IReadOnlyCollection<BackendInstance> live = supervisor.All;

            return Results.Json(manifest.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    pair.Value.Pool,
                    pair.Value.ActiveVersion,
                    pair.Value.OverlapAllowed,
                    pair.Value.EagerStart,
                    pair.Value.IdleTimeoutMinutes,
                    backends = live
                        .Where(instance => instance.Key.Server == pair.Key)
                        .Select(instance => new
                        {
                            poolKey = instance.Key.PoolKey,
                            instance.Version,
                            instance.Port,
                            pid = instance.Handle.ProcessId,
                            instance.InFlight,
                            lastUsedAt = instance.LastUsedAt
                        })
                        .ToList()
                }));
        });

        app.MapPost("/admin/servers/{name}/activate", async (
            string name,
            ActivateRequest body,
            ActivationService activation,
            ManifestStore manifest,
            CancellationToken cancellationToken) =>
        {
            if (!manifest.TryGet(name, out _)) return Results.NotFound($"No server named '{name}'.");

            ActivationResult result = await activation.ActivateAsync(
                name, body.Version, cancellationToken);

            return result.Succeeded
                ? Results.Json(result)
                : Results.Json(result, statusCode: StatusCodes.Status409Conflict);
        });

        app.MapPost("/admin/servers/{name}/stop", async (
            string name,
            BackendSupervisor supervisor,
            ManifestStore manifest,
            CancellationToken cancellationToken) =>
        {
            if (!manifest.TryGet(name, out _)) return Results.NotFound($"No server named '{name}'.");

            List<BackendKey> keys = supervisor.All
                .Where(instance => instance.Key.Server == name)
                .Select(instance => instance.Key)
                .ToList();

            foreach (BackendKey key in keys) await supervisor.StopAsync(key, cancellationToken);

            return Results.Json(new { stopped = keys.Count });
        });

        app.MapPost("/admin/servers/{name}/restart", async (
            string name,
            BackendSupervisor supervisor,
            ManifestStore manifest,
            CancellationToken cancellationToken) =>
        {
            if (!manifest.TryGet(name, out ServerEntry? entry))
            {
                return Results.NotFound($"No server named '{name}'.");
            }

            List<BackendKey> keys = supervisor.All
                .Where(instance => instance.Key.Server == name)
                .Select(instance => instance.Key)
                .ToList();

            foreach (BackendKey key in keys)
            {
                await supervisor.StopAsync(key, cancellationToken);
                await supervisor.GetOrStartAsync(key, cancellationToken);
            }

            return Results.Json(new { restarted = keys.Count, entry!.ActiveVersion });
        });

        app.MapPost("/admin/prune", async (
            ManifestStore manifest,
            BackendSupervisor supervisor,
            CancellationToken cancellationToken) =>
        {
            var pruned = new Dictionary<string, IReadOnlyList<string>>();

            foreach (string server in manifest.Entries.Keys)
            {
                pruned[server] = await supervisor.PruneVersionsAsync(server, cancellationToken);
            }

            return Results.Json(pruned);
        });

        return app;
    }
}
```

In `McpGateway/GatewayApp.cs`, add `using McpGateway.Endpoints;`, delete the inline `/admin/servers` mapping added in Task 3, and replace it with:

```csharp
        app.MapAdminEndpoints();
```

- [ ] **Step 10: Run everything**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: all green, with 5 more tests than after Step 5 (8 new in this task).

- [ ] **Step 11: Verify the admin tests can fail**

In the activate endpoint, change the failure branch to always return `Results.Json(result)` and rerun. Expected: `Activate_Returns409_WhenTheNewVersionFailsToStart` goes red. Restore it.

In `MapGet("/admin/servers")`, hardcode `backends` to an empty list and rerun. Expected: `Servers_ReportsLiveBackendState` goes red. Restore it.

- [ ] **Step 12: Commit**

```bash
git add McpGateway McpGateway.Tests
git commit -m "feat: hold-and-swap activation for exclusive servers, plus admin API"
```

---

### Task 9: Deploy pipeline and a real end-to-end swap

Everything so far ran against a fake launcher. This task publishes a real MCP server built on
`Mcp.Hosting.Core`, spawns it with the real `ProcessBackendLauncher`, and swaps versions under
continuous load — the first test that would catch a broken port-file handshake or a wrong
`--mcp-port-file` contract.

**Files:**
- Create: `McpGateway.TestBackend/McpGateway.TestBackend.csproj`
- Create: `McpGateway.TestBackend/Program.cs`
- Create: `McpGateway.TestBackend/EchoTools.cs`
- Create: `McpGateway.Tests/EndToEndSwapTests.cs`
- Create: `build/publish.ps1`
- Create: `build/activate.ps1`
- Create: `build/deploy.ps1`
- Create: `build/register-gateway-task.ps1`
- Modify: `McpServers.slnx`

**Interfaces:**
- Consumes: `McpHttpHost.CreateBuilder`, `MapMcpHost` (Task 2); `ProcessBackendLauncher`, `BackendSupervisor` (Task 4); `ActivationService`, admin endpoints (Tasks 7–8).
- Produces:
  - `build/publish.ps1 -Server <name>` → prints the new version id, publishes to `deploy/<server>/<version>`
  - `build/activate.ps1 -Server <name> -Version <id>` → POSTs to the gateway, fails the script on a 409
  - `build/deploy.ps1 -Server <name>` → publish then activate; the one command Task 13 and Stage 3 use
  - `build/register-gateway-task.ps1` → Task Scheduler logon entry
  - `McpGateway.TestBackend` — a real MCP server exposing `echo_version`, used only by tests

- [ ] **Step 1: Create the test backend**

Create `McpGateway.TestBackend/McpGateway.TestBackend.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="ModelContextProtocol" />
        <PackageReference Include="ModelContextProtocol.AspNetCore" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Libraries\Mcp.Hosting.Core\Mcp.Hosting.Core.csproj" />
    </ItemGroup>

</Project>
```

Create `McpGateway.TestBackend/EchoTools.cs`:

```csharp
using System.ComponentModel;
using Mcp.Hosting.Core;
using ModelContextProtocol.Server;

namespace McpGateway.TestBackend;

[McpServerToolType]
public class EchoTools(McpHostOptions options)
{
    [McpServerTool, DisplayName("echo_version")]
    [Description("Returns the version this backend was started with, and the calling client id.")]
    public string EchoVersion() => $"{options.Version}|{McpCaller.ClientId}";
}
```

Create `McpGateway.TestBackend/Program.cs`:

```csharp
using Mcp.Hosting.Core;
using McpGateway.TestBackend;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;

WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "test-backend");

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
    .WithTools<EchoTools>();

WebApplication app = builder.Build();
app.MapMcpHost();

await app.RunAsync();
```

Add to `McpServers.slnx`:

```xml
  <Project Path="McpGateway.TestBackend\McpGateway.TestBackend.csproj" />
```

- [ ] **Step 2: Write the failing end-to-end test**

Create `McpGateway.Tests/EndToEndSwapTests.cs`:

```csharp
using System.Diagnostics;
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

    private async Task<string> CallEchoAsync()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/echo/mcp", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = "echo_version", arguments = new { } }
        }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        return body;
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
        });

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
```

Add the project reference so the test project builds the backend first. In `McpGateway.Tests/McpGateway.Tests.csproj`:

```xml
        <ProjectReference Include="..\McpGateway.TestBackend\McpGateway.TestBackend.csproj" />
```

- [ ] **Step 3: Run it**

```powershell
dotnet build McpGateway.Tests\McpGateway.Tests.csproj -c Debug -m:1 -v quiet
dotnet McpGateway.Tests\bin\Debug\net10.0\McpGateway.Tests.dll -noColor
```

Expected: all green, with 2 more tests than before this task. If `Swap_UnderContinuousLoad_LosesNoCalls` fails, the failure message names the first three errors — the likely causes are the port-file path contract, `MCP_SERVER_VERSION` not reaching the backend, or the forwarder's activity timeout.

- [ ] **Step 4: Verify the end-to-end test can fail**

In `ActivationService.ActivateAsync`, move `await old.StopAsync(cancellationToken);` to *before*
`supervisor.Replace(old.Key, replacement);` and rerun. Expected: `Swap_UnderContinuousLoad_LosesNoCalls`
goes red with connection failures — proving the test actually detects a badly ordered swap rather
than passing on timing luck. Restore the order.

- [ ] **Step 5: Write the publish script**

Create `build/publish.ps1`:

```powershell
#requires -Version 7
<#
.SYNOPSIS
Publishes one MCP server to a fresh versioned deploy directory and prints the version id.

.DESCRIPTION
Nothing runs out of bin/ any more. Each publish gets its own directory, so a running backend never
holds a lock on the files a rebuild wants to write.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Server,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$manifest = Get-Content (Join-Path $repoRoot 'servers.json') -Raw | ConvertFrom-Json
$entry = $manifest.$Server
if (-not $entry) { throw "No server named '$Server' in servers.json." }

$sha = (git -C $repoRoot rev-parse --short HEAD).Trim()
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmm')
$version = "v-$sha-$stamp"

$output = Join-Path $repoRoot (Join-Path $entry.deployRoot $version)
$project = Join-Path $repoRoot $entry.project

Write-Host "Publishing $Server -> $output"
dotnet publish $project -c $Configuration -o $output --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Server." }

$assembly = Join-Path $output $entry.assembly
if (-not (Test-Path $assembly)) { throw "Publish produced no $($entry.assembly) at $output." }

Write-Output $version
```

- [ ] **Step 6: Write the activate and deploy scripts**

Create `build/activate.ps1`:

```powershell
#requires -Version 7
<#
.SYNOPSIS
Asks the running gateway to swap a server to a published version.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Server,
    [Parameter(Mandatory)][string] $Version,
    [string] $GatewayUrl = 'http://127.0.0.1:7300'
)

$ErrorActionPreference = 'Stop'

$tokenPath = Join-Path $env:LOCALAPPDATA 'McpGateway\token'
if (-not (Test-Path $tokenPath)) { throw "No gateway token at $tokenPath. Is the gateway running?" }
$token = (Get-Content $tokenPath -Raw).Trim()

try {
    $response = Invoke-RestMethod -Method Post `
        -Uri "$GatewayUrl/admin/servers/$Server/activate" `
        -Headers @{ Authorization = "Bearer $token" } `
        -ContentType 'application/json' `
        -Body (@{ version = $Version } | ConvertTo-Json)
}
catch {
    $detail = $_.ErrorDetails.Message
    throw "Activation of $Server -> $Version was refused: $detail"
}

Write-Host "Activated $Server $($response.fromVersion) -> $($response.toVersion), $($response.backendsSwapped) backend(s) swapped."
if ($response.drainTimedOut) {
    Write-Warning 'Drain timed out; an in-flight call may have been cut off.'
}
```

Create `build/deploy.ps1`:

```powershell
#requires -Version 7
<#
.SYNOPSIS
Publish then activate. The one command to upgrade a server without stopping any client.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Server,
    [string] $Configuration = 'Release',
    [string] $GatewayUrl = 'http://127.0.0.1:7300'
)

$ErrorActionPreference = 'Stop'

$version = & (Join-Path $PSScriptRoot 'publish.ps1') -Server $Server -Configuration $Configuration
& (Join-Path $PSScriptRoot 'activate.ps1') -Server $Server -Version $version -GatewayUrl $GatewayUrl
```

- [ ] **Step 7: Write the Task Scheduler registration**

Windows Services would run as SYSTEM, which breaks the servers that need the interactive user
session and user-profile credentials — browser automation, desktop automation, SSH keys, AWS and
Azure credential stores. A logon task runs as the user.

Create `build/register-gateway-task.ps1`:

```powershell
#requires -Version 7
<#
.SYNOPSIS
Registers the gateway to start at logon as the current user.
#>
[CmdletBinding()]
param(
    [string] $TaskName = 'McpGateway',
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$publishRoot = Join-Path $repoRoot 'deploy\_gateway\current'
Write-Host "Publishing gateway -> $publishRoot"
dotnet publish (Join-Path $repoRoot 'McpGateway\McpGateway.csproj') `
    -c $Configuration -o $publishRoot --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Gateway publish failed.' }

$action = New-ScheduledTaskAction `
    -Execute 'dotnet' `
    -Argument "`"$publishRoot\McpGateway.dll`"" `
    -WorkingDirectory $repoRoot

$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero)

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -Force -RunLevel Limited | Out-Null

Write-Host "Registered '$TaskName'. Start it now with: Start-ScheduledTask -TaskName $TaskName"
```

The gateway reads `MCP_GATEWAY_REPO_ROOT` or falls back to its working directory, which the task
sets to the repo root.

- [ ] **Step 8: Verify the scripts against the test backend**

Add `echo` to `servers.json` temporarily so the scripts have a real target:

```powershell
$env:MCP_GATEWAY_REPO_ROOT = "C:\Users\jorda\RiderProjects\McpServers"
dotnet run --project McpGateway\McpGateway.csproj
```

In a second shell, add an `echo` entry pointing at `McpGateway.TestBackend`, then:

```powershell
cd C:\Users\jorda\RiderProjects\McpServers
.\build\deploy.ps1 -Server echo -Configuration Debug
```

Expected: a version id is printed, then `Activated echo unset -> v-...`. Call it through the
gateway:

```powershell
$t = Get-Content "$env:LOCALAPPDATA\McpGateway\token" -Raw
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:7300/echo/mcp `
  -Headers @{ Authorization = "Bearer $($t.Trim())"; 'X-Mcp-Client' = 'manual' } `
  -ContentType 'application/json' `
  -Body '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"echo_version","arguments":{}}}'
```

Expected: the response contains the published version id and `manual`.

Now run `deploy.ps1` again while that call loops, and confirm no failures. Then remove the temporary
`echo` entry from `servers.json` and stop the gateway.

- [ ] **Step 9: Commit**

```bash
git add McpGateway McpGateway.Tests McpGateway.TestBackend build McpServers.slnx
git commit -m "feat: add deploy pipeline and end-to-end swap coverage"
```

---

## Stage 2 — CodeAssist

Two latent bugs first. Both are harmless while every session gets its own process and become live
hazards the moment CodeAssist is one shared instance.

### Task 10: `IndexStateStore.Delete` takes the write lock

> **The concurrency test below is insufficient — see commits after `dfe69cf`.** As written,
> `DeleteAsync_DoesNotBreakAConcurrentSave` cannot fail even with the lock removed, for two reasons
> visible in its own source. Its `StateFor` helper writes a tiny file, where the proven sibling
> `LoadAsync_NeverBlocksAConcurrentSave` pads `RootPath` to 4 MB precisely so a read is still in
> flight when the write lands. And it only fails if `SaveAsync` throws, which requires exhausting
> `WriteAtomicAsync`'s whole `[20, 50, 100, 200, 400]`ms retry ladder — so a genuine collision is
> retried away silently, and its `NullLogger` never observes the warning that would prove a
> near-miss. The sibling instead fails on a single logged warning via a `RecordingLogger`. The
> shipped version borrows that technique and adds a deterministic mutual-exclusion test.

`Libraries/CodeAssist.Core/Services/IndexStateStore.cs:220` is the one method on that class that
doesn't take `_writeLock`. `LoadAsync`, `SaveAsync`, `TouchAsync` and `WriteAtomicAsync` all do, and
the class's own comment explains why: Windows won't move a file onto a name a reader holds open, so
a lock-free reader isn't a passive observer — it's the thing that breaks a concurrent write.
`Delete` reads the file with `File.ReadAllText` before deleting it, which is exactly that reader.

**Files:**
- Modify: `Libraries/CodeAssist.Core/Services/IndexStateStore.cs:220`
- Modify: `Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs:411`
- Create: `Libraries/CodeAssist.Core.Tests/Services/IndexStateStoreDeleteTests.cs`

**Interfaces:**
- Consumes: `IndexStateStore`, `IndexStateFile`, `CodeAssistOptions` (existing).
- Produces: `IndexStateStore.DeleteAsync(string repositoryName, CancellationToken) -> Task`, replacing the synchronous `Delete`. `RepositoryIndexer.DeleteIndexAsync` is its only caller.

- [ ] **Step 1: Write the failing test**

One round proves nothing here — a race that loses sometimes still loses. The existing fix for this
class was measured over twenty rounds, so match that.

Create `Libraries/CodeAssist.Core.Tests/Services/IndexStateStoreDeleteTests.cs`:

```csharp
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public sealed class IndexStateStoreDeleteTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "codeassist-delete-" + Guid.NewGuid().ToString("N"));

    private IndexStateStore MakeStore() => new(
        Options.Create(new CodeAssistOptions { IndexStateDirectory = _directory }),
        NullLogger<IndexStateStore>.Instance);

    // IndexStateFile has nine required members; all of them must be set for this to compile.
    private static IndexStateFile StateFor(string repository) => new()
    {
        RepositoryName = repository,
        RootPath = @"C:\repo",
        CreatedAt = DateTimeOffset.UtcNow,
        LastUpdatedAt = DateTimeOffset.UtcNow,
        EmbeddingModel = "test-model",
        CollectionName = CollectionNaming.ForRepository(repository),
        IncludePatterns = ["**/*.cs"],
        ExcludePatterns = [],
        Files = []
    };

    [Fact]
    public async Task DeleteAsync_RemovesTheStateFile()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("Repo", StateFor("Repo"), TestContext.Current.CancellationToken);

        await store.DeleteAsync("Repo", TestContext.Current.CancellationToken);

        Assert.Null(await store.LoadAsync("Repo", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_StillRejectsAMismatchedRepositoryName()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("Repo", StateFor("Repo"), TestContext.Current.CancellationToken);

        // Rewrite the file's own idea of which repository it belongs to. Deleting by the path's
        // name must refuse rather than destroy another repository's state.
        string path = store.GetStatePath("Repo");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("\"RepositoryName\": \"Repo\"", "\"RepositoryName\": \"Different\""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.DeleteAsync("Repo", TestContext.Current.CancellationToken));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotBreakAConcurrentSave()
    {
        IndexStateStore store = MakeStore();
        var saveFailures = new List<string>();

        // Twenty rounds, matching the measurement that established this class's locking rule.
        for (var round = 0; round < 20; round++)
        {
            string repository = $"Repo{round}";
            await store.SaveAsync(
                repository, StateFor(repository), TestContext.Current.CancellationToken);

            Task saving = Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < 10; i++)
                    {
                        await store.SaveAsync(
                            repository, StateFor(repository),
                            TestContext.Current.CancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    lock (saveFailures) saveFailures.Add($"round {round}: {ex.Message}");
                }
            });

            Task deleting = Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    try
                    {
                        await store.DeleteAsync(
                            repository, TestContext.Current.CancellationToken);
                    }
                    catch (InvalidOperationException)
                    {
                        // Name validation, not a race.
                    }
                }
            });

            await Task.WhenAll(saving, deleting);
        }

        Assert.True(saveFailures.Count == 0,
            $"{saveFailures.Count} save(s) lost to a concurrent delete: "
            + string.Join("; ", saveFailures.Take(3)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```powershell
dotnet build Libraries\CodeAssist.Core.Tests\CodeAssist.Core.Tests.csproj -c Debug -m:1 -v quiet
```

Expected: FAIL to compile — `DeleteAsync` does not exist.

- [ ] **Step 3: Convert `Delete` to a locking `DeleteAsync`**

In `Libraries/CodeAssist.Core/Services/IndexStateStore.cs`, replace the whole `Delete` method
(starting at line 220) with:

```csharp
    /// <summary>
    /// Remove a repository's state file.
    /// </summary>
    /// <remarks>
    /// Takes the write lock for the same reason <see cref="LoadAsync"/> does: this method reads the
    /// file before deleting it, and on Windows a reader holding the path open is enough to fail a
    /// concurrent <see cref="WriteAtomicAsync"/> move.
    /// </remarks>
    public async Task DeleteAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        string path = GetStatePath(repositoryName);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path)) return;

            IndexStateFile? state = JsonSerializer.Deserialize<IndexStateFile>(
                await File.ReadAllTextAsync(path, cancellationToken));

            ValidateRepositoryName(repositoryName, state);
            File.Delete(path);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the caller's decision, not a delete failure. Swallowing it here
            // would report a cancelled delete as an unlucky one, with only a log line to see.
            // ListRepositoryNamesAsync already rethrows it ahead of its generic catch.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete index state at {Path}", path);
        }
        finally
        {
            _writeLock.Release();
        }
    }
```

- [ ] **Step 4: Update the only caller**

In `Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs:411`, replace:

```csharp
        indexStateStore.Delete(repositoryName);
```

with:

```csharp
        await indexStateStore.DeleteAsync(repositoryName, cancellationToken);
```

Confirm there are no others:

```powershell
Select-String -Path (Get-ChildItem -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) -Pattern 'indexStateStore\.Delete\(' 
```

Expected: no matches.

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet build Libraries\CodeAssist.Core.Tests\CodeAssist.Core.Tests.csproj -c Debug -m:1 -v quiet
dotnet Libraries\CodeAssist.Core.Tests\bin\Debug\net10.0\CodeAssist.Core.Tests.dll -noColor
```

Expected: every existing test still passes, plus the 3 new ones.

- [ ] **Step 6: Verify the concurrency test can fail**

Remove the `await _writeLock.WaitAsync(cancellationToken);` and matching `_writeLock.Release();`
from `DeleteAsync`, rebuild and rerun. Expected: `DeleteAsync_DoesNotBreakAConcurrentSave` goes red.
If it does not, raise the round count until it does before restoring the lock — a race test that
never fails without the fix is not a test. Restore the lock afterwards.

- [ ] **Step 7: Commit**

```bash
git add Libraries/CodeAssist.Core/Services/IndexStateStore.cs Libraries/CodeAssist.Core/Services/RepositoryIndexer.cs Libraries/CodeAssist.Core.Tests/Services/IndexStateStoreDeleteTests.cs
git commit -m "fix: take the write lock when deleting index state"
```

---

### Task 11: `set_active_repository` stops clearing other sessions' caches

`CodeAssistMcp/McpTools/RepositoryTools.cs:29` declares `bool clearOtherCaches = true`. The tool
stops watching every other repository and, by default, wipes their L1 caches. With one process per
session that only affected the caller. Shared, it means one session switching projects silently
kills another session's watcher and cache.

The watcher change is the tool's stated purpose and stays. The cache wipe becomes opt-in.

**Files:**
- Modify: `CodeAssistMcp/McpTools/RepositoryTools.cs:29-31`
- Create: `Libraries/CodeAssist.Core.Tests/Services/HotCacheClearScopeTests.cs`

**Interfaces:**
- Consumes: `RepositoryTools.SetActiveRepository(string repositoryName, bool clearOtherCaches)` (existing signature, default changes).
- Produces: no signature change — `clearOtherCaches` defaults to `false`. Callers that want the old behaviour pass `true` explicitly.

- [ ] **Step 1: Read the current behaviour**

```powershell
Get-Content CodeAssistMcp\McpTools\RepositoryTools.cs | Select-Object -Skip 25 -First 60
```

Confirm the default is `true` and that `clearOtherCaches` guards a `hotCache` clear inside the
`foreach (string watchedPath in currentlyWatched)` loop.

- [ ] **Step 2: Write the failing test**

This one asserts on the parameter's default via reflection, because the destructive behaviour lives
behind live services that a unit test shouldn't stand up.

Create `Libraries/CodeAssist.Core.Tests/Services/HotCacheClearScopeTests.cs`:

```csharp
using System.Reflection;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

/// <summary>
/// set_active_repository wipes other repositories' caches. Harmless when every session had its own
/// process; destructive once CodeAssist is one shared instance. The default must be opt-in.
/// </summary>
public sealed class HotCacheClearScopeTests
{
    [Fact]
    public void SetActiveRepository_DoesNotClearOtherCachesByDefault()
    {
        Type toolsType = Assembly.Load("CodeAssistMcp")
            .GetType("CodeAssistMcp.McpTools.RepositoryTools")
            ?? throw new InvalidOperationException("RepositoryTools not found.");

        MethodInfo method = toolsType.GetMethod("SetActiveRepository")
            ?? throw new InvalidOperationException("SetActiveRepository not found.");

        ParameterInfo parameter = method.GetParameters()
            .Single(p => p.Name == "clearOtherCaches");

        Assert.True(parameter.HasDefaultValue);
        Assert.Equal(false, parameter.DefaultValue);
    }
}
```

Add the project reference to `Libraries/CodeAssist.Core.Tests/CodeAssist.Core.Tests.csproj`:

```xml
        <ProjectReference Include="..\..\CodeAssistMcp\CodeAssistMcp.csproj" />
```

- [ ] **Step 3: Run to verify it fails**

```powershell
dotnet build Libraries\CodeAssist.Core.Tests\CodeAssist.Core.Tests.csproj -c Debug -m:1 -v quiet
dotnet Libraries\CodeAssist.Core.Tests\bin\Debug\net10.0\CodeAssist.Core.Tests.dll -noColor -method "*HotCacheClearScopeTests*"
```

Expected: FAIL — `Assert.Equal(false, parameter.DefaultValue)` sees `True`.

- [ ] **Step 4: Flip the default and say why in the description**

In `CodeAssistMcp/McpTools/RepositoryTools.cs`, replace the attribute and signature at lines 27–31:

```csharp
    [McpServerTool, DisplayName("set_active_repository")]
    [Description("Set the active repository for file watching. Stops watching all other repositories and starts watching the specified one. Use this when switching between projects to ensure only the current project is monitored for changes. Set clearOtherCaches to true to also drop other repositories' in-memory caches — this affects every session sharing this server, so leave it off unless you mean it.")]
    public async Task<string> SetActiveRepository(
        string repositoryName,
        bool clearOtherCaches = false)
```

- [ ] **Step 5: Run to verify it passes**

```powershell
dotnet build Libraries\CodeAssist.Core.Tests\CodeAssist.Core.Tests.csproj -c Debug -m:1 -v quiet
dotnet Libraries\CodeAssist.Core.Tests\bin\Debug\net10.0\CodeAssist.Core.Tests.dll -noColor
```

Expected: all pass.

- [ ] **Step 6: Verify the test can fail**

Set the default back to `true`, rerun, confirm red, then set it to `false` again.

- [ ] **Step 7: Commit**

```bash
git add CodeAssistMcp/McpTools/RepositoryTools.cs Libraries/CodeAssist.Core.Tests
git commit -m "fix: make set_active_repository cache clearing opt-in"
```

---

### Task 12: Convert CodeAssist to HTTP

The tool surface must not change. Capture it from the stdio build first, convert, then diff — that
baseline is also the regression gate Stage 3 uses for the other thirteen servers.

**Files:**
- Create: `build/dump-tools-stdio.ps1`
- Create: `build/dump-tools-http.ps1`
- Create: `build/tool-baselines/code-assist.json` (generated in Step 1)
- Modify: `CodeAssistMcp/CodeAssistMcp.csproj`
- Modify: `CodeAssistMcp/Program.cs`

**Interfaces:**
- Consumes: `McpHttpHost.CreateBuilder`, `MapMcpHost` (Task 2); the deploy scripts (Task 9).
- Produces:
  - `build/dump-tools-stdio.ps1 -Assembly <path>` → sorted `tools/list` JSON on stdout
  - `build/dump-tools-http.ps1 -Server <name>` → the same shape, through the gateway
  - `build/tool-baselines/code-assist.json` — the committed golden file Stage 3 compares against

- [ ] **Step 1: Capture the stdio tool baseline before touching anything**

Create `build/dump-tools-stdio.ps1`:

```powershell
#requires -Version 7
<#
.SYNOPSIS
Drives a stdio MCP server through initialize + tools/list and prints its tool surface, sorted.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Assembly)

$ErrorActionPreference = 'Stop'

$psi = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
$psi.ArgumentList.Add($Assembly)
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.UseShellExecute = $false

$process = [System.Diagnostics.Process]::Start($psi)

function Send($obj) {
    $process.StandardInput.WriteLine(($obj | ConvertTo-Json -Depth 12 -Compress))
    $process.StandardInput.Flush()
}

function ReadResult {
    while ($true) {
        $line = $process.StandardOutput.ReadLine()
        if ($null -eq $line) { throw 'Server closed stdout before responding.' }
        if ($line.Trim().Length -eq 0) { continue }
        $msg = $line | ConvertFrom-Json
        if ($msg.PSObject.Properties.Name -contains 'result') { return $msg.result }
        if ($msg.PSObject.Properties.Name -contains 'error') { throw "Server error: $($msg.error.message)" }
    }
}

Send @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{
    protocolVersion = '2025-11-25'; capabilities = @{}
    clientInfo = @{ name = 'tool-parity'; version = '1' } } }
ReadResult | Out-Null

Send @{ jsonrpc = '2.0'; method = 'notifications/initialized' }
Send @{ jsonrpc = '2.0'; id = 2; method = 'tools/list' }
$result = ReadResult

try { $process.Kill($true) } catch { }

$result.tools |
    Sort-Object name |
    Select-Object name, description, inputSchema |
    ConvertTo-Json -Depth 20
```

Run it against a **Debug** build and commit the result as the baseline:

```powershell
dotnet build CodeAssistMcp\CodeAssistMcp.csproj -c Debug --no-restore -m:1 -v quiet
New-Item -ItemType Directory -Force build\tool-baselines | Out-Null
.\build\dump-tools-stdio.ps1 -Assembly `
  "C:\Users\jorda\RiderProjects\McpServers\CodeAssistMcp\bin\Debug\net10.0\CodeAssistMcp.dll" `
  | Set-Content build\tool-baselines\code-assist.json
```

**Two reasons this uses Debug, not Release.** First, the Release DLL is serving live sessions;
spawning a second instance of it starts a full CodeAssist with its watcher startup service against
the same machine-wide `%LocalAppData%\CodeAssist\indexes`, which is not a read-only act. Second, and
more important for correctness: this baseline must be captured **after** Tasks 10 and 11 land,
because Task 11 deliberately changes `set_active_repository`'s `clearOtherCaches` default from
`true` to `false` — and a parameter default is part of the tool's input schema. A baseline taken
from the untouched Release build would show that intentional change as a conversion diff, which is
exactly the signal this baseline exists to isolate. Capture it from a Debug build of the code as it
stands after Task 11, so the only thing the Step 5 diff can reveal is whether the HTTP conversion
itself altered the tool surface.

Confirm it lists the expected tools:

```powershell
(Get-Content build\tool-baselines\code-assist.json -Raw | ConvertFrom-Json).name
```

Expected: the CodeAssist tool names — `check_health`, `search_code`, `index_repository`,
`set_active_repository`, `trace_data_flow` and the rest. If the file is empty or the script hangs,
stop and fix the script before converting anything; the baseline is the only evidence the
conversion preserved the surface.

```bash
git add build/dump-tools-stdio.ps1 build/tool-baselines/code-assist.json
git commit -m "test: capture CodeAssist stdio tool surface as a conversion baseline"
```

- [ ] **Step 2: Convert the project file**

In `CodeAssistMcp/CodeAssistMcp.csproj`, change the SDK on line 1 from `Microsoft.NET.Sdk` to
`Microsoft.NET.Sdk.Web`, and add the hosting library and ASP.NET MCP package:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
      <ProjectReference Include="..\Libraries\CodeAssist.Core\CodeAssist.Core.csproj" />
      <ProjectReference Include="..\Libraries\Mcp.Common.Core\Mcp.Common.Core.csproj" />
      <ProjectReference Include="..\Libraries\Mcp.Hosting.Core\Mcp.Hosting.Core.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="ModelContextProtocol" />
      <PackageReference Include="ModelContextProtocol.AspNetCore" />
    </ItemGroup>

    <ItemGroup>
      <None Update="appsettings.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      </None>
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Convert `Program.cs`**

Replace the whole of `CodeAssistMcp/Program.cs` with:

```csharp
using CodeAssist.Core.Extensions;
using CodeAssistMcp.McpTools;
using CodeAssistMcp.Services;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in
    // McpHttpHost. Log.Logger is configured by the time this returns.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "code-assist");

    Log.Information("Starting CodeAssist MCP server");

    builder.Services.AddCodeAssistServices(builder.Configuration);
    builder.Services.AddSingleton<RepositoryWatcherStartupService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RepositoryWatcherStartupService>());

    builder.Services.AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<HealthTools>()
        .WithTools<IndexTools>()
        .WithTools<SearchTools>()
        .WithTools<RepositoryTools>()
        .WithTools<PersonalContextTools>()
        .WithTools<DataFlowTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CodeAssist MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

- [ ] **Step 4: Build in Debug and confirm it starts**

Debug only. The Release DLL is still in use by live sessions and must not be rebuilt yet.

```powershell
dotnet build CodeAssistMcp\CodeAssistMcp.csproj -c Debug --no-restore -m:1 -v minimal
```

Expected: 0 warnings, 0 errors.

```powershell
$portFile = Join-Path $env:TEMP 'codeassist-manual-port.json'
$env:MCP_SERVER_VERSION = 'manual'

# Isolate this instance's state. Starting CodeAssist runs RepositoryWatcherStartupService, which
# reads and writes the active-repository file under IndexStateDirectory. The live Release servers
# are using the real one, and IndexStateStore's locks are in-process SemaphoreSlims that give no
# protection across processes -- so point this run at a throwaway directory.
$env:CodeAssist__IndexStateDirectory = Join-Path $env:TEMP 'codeassist-manual-state'

dotnet CodeAssistMcp\bin\Debug\net10.0\CodeAssistMcp.dll --mcp-port-file $portFile
```

Clear `CodeAssist__IndexStateDirectory` and delete that directory when you are done, so a later run
does not silently inherit it.

In a second shell:

```powershell
$p = (Get-Content (Join-Path $env:TEMP 'codeassist-manual-port.json') -Raw | ConvertFrom-Json).Port
Invoke-RestMethod "http://127.0.0.1:$p/health"
```

Expected: `status` of `ok`, `name` of `code-assist`, `version` of `manual`. Leave it running for the
next step.

- [ ] **Step 5: Write the HTTP tool dumper and diff against the baseline**

Create `build/dump-tools-http.ps1`:

```powershell
#requires -Version 7
<#
.SYNOPSIS
Drives an HTTP MCP endpoint through initialize + tools/list and prints its tool surface, sorted.

.DESCRIPTION
Uses the 2025-11-25 handshake, which StatefulForInitializeClients still serves, so the output is
directly comparable with the stdio baseline.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Url,
    [hashtable] $Headers = @{}
)

$ErrorActionPreference = 'Stop'

$init = @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{
    protocolVersion = '2025-11-25'; capabilities = @{}
    clientInfo = @{ name = 'tool-parity'; version = '1' } } } | ConvertTo-Json -Depth 12

$response = Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers `
    -ContentType 'application/json' -Body $init

$sessionId = $response.Headers['Mcp-Session-Id']
if ($sessionId) { $Headers['Mcp-Session-Id'] = [string]$sessionId }

Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers -ContentType 'application/json' `
    -Body (@{ jsonrpc = '2.0'; method = 'notifications/initialized' } | ConvertTo-Json) | Out-Null

$list = Invoke-RestMethod -Method Post -Uri $Url -Headers $Headers `
    -ContentType 'application/json' `
    -Body (@{ jsonrpc = '2.0'; id = 2; method = 'tools/list' } | ConvertTo-Json)

$list.result.tools |
    Sort-Object name |
    Select-Object name, description, inputSchema |
    ConvertTo-Json -Depth 20
```

Point it at the process still running from Step 4 and compare:

```powershell
$p = (Get-Content (Join-Path $env:TEMP 'codeassist-manual-port.json') -Raw | ConvertFrom-Json).Port
.\build\dump-tools-http.ps1 -Url "http://127.0.0.1:$p/mcp" | Set-Content $env:TEMP\ca-http.json

$before = Get-Content build\tool-baselines\code-assist.json -Raw
$after  = Get-Content $env:TEMP\ca-http.json -Raw
if ($before -ne $after) {
    Compare-Object ($before -split "`n") ($after -split "`n") | Format-Table -AutoSize
    throw 'Tool surface changed.'
}
Write-Host 'Tool surface identical.'
```

Expected: `Tool surface identical.` A difference here means the conversion changed what the model
sees, which is a conversion bug — fix it before continuing. Stop the manual process afterwards, and
delete `$env:TEMP\ca-http.json` and the port file.

- [ ] **Step 6: Run the full CodeAssist test suite**

```powershell
dotnet build Libraries\CodeAssist.Core.Tests\CodeAssist.Core.Tests.csproj -c Debug --no-restore -m:1 -v quiet
dotnet Libraries\CodeAssist.Core.Tests\bin\Debug\net10.0\CodeAssist.Core.Tests.dll -noColor
```

Expected: all pass, including Tasks 10 and 11's additions.

- [ ] **Step 7: Commit**

```bash
git add CodeAssistMcp/CodeAssistMcp.csproj CodeAssistMcp/Program.cs build/dump-tools-http.ps1
git commit -m "feat: serve CodeAssist over HTTP via Mcp.Hosting.Core"
```

---

### Task 13: Cutover

Publishes CodeAssist to a deploy directory, puts it behind the gateway, and repoints both clients.
This is the step that changes the user's live configuration.

**Files:**
- Modify: `servers.json` (`activeVersion`)
- Modify: `C:\Users\jorda\.claude.json` (the `code-assist` entry)
- Modify: `C:\Users\jorda\AppData\Roaming\Claude\claude_desktop_config.json` (the `code-assist` entry)

**Interfaces:**
- Consumes: `build/deploy.ps1`, `build/register-gateway-task.ps1` (Task 9); the converted CodeAssist (Task 12).
- Produces: a live gateway-backed `code-assist`. Stage 3 repeats Steps 3–6 per server.

- [ ] **Step 1: STOP — get explicit approval**

Do not run any later step until the user says yes. This step edits live client configuration and
requires restarting their sessions, and CodeAssist's Release DLL is under a standing instruction not
to be rebuilt or restarted without approval.

Tell the user exactly what will happen: the gateway gets registered as a logon task and started; a
Release publish of CodeAssist lands in `deploy/code-assist/<version>`; both client configs get their
`code-assist` entry rewritten to `http://127.0.0.1:7300/code-assist/mcp` with two headers; and every
open Claude Code and Claude Desktop session needs restarting before the change takes effect. Note
that the old stdio entry can be restored from the backup written in Step 4.

- [ ] **Step 2: Register and start the gateway**

```powershell
cd C:\Users\jorda\RiderProjects\McpServers
.\build\register-gateway-task.ps1
Start-ScheduledTask -TaskName McpGateway
```

Verify:

```powershell
$t = (Get-Content "$env:LOCALAPPDATA\McpGateway\token" -Raw).Trim()
Invoke-RestMethod http://127.0.0.1:7300/admin/servers -Headers @{ Authorization = "Bearer $t" }
```

Expected: the `code-assist` entry with `activeVersion` of `unset` and an empty `backends` array.

- [ ] **Step 3: Publish CodeAssist and activate it**

```powershell
.\build\deploy.ps1 -Server code-assist
```

Expected: a version id, then `Activated code-assist unset -> v-...`. Because no backend was running,
`backendsSwapped` is 0.

Confirm it serves:

```powershell
$t = (Get-Content "$env:LOCALAPPDATA\McpGateway\token" -Raw).Trim()
Invoke-RestMethod http://127.0.0.1:7300/code-assist/health -Headers @{ Authorization = "Bearer $t" }
```

Expected: `status` of `ok` and the published version id. First call may take up to
`startupTimeoutSeconds` — the gateway holds it rather than failing.

- [ ] **Step 4: Back up both client configs**

```powershell
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmm')
Copy-Item C:\Users\jorda\.claude.json "$env:TEMP\claude.json.$stamp.bak"
Copy-Item C:\Users\jorda\AppData\Roaming\Claude\claude_desktop_config.json `
          "$env:TEMP\claude_desktop_config.json.$stamp.bak"
Write-Host "Backups written to $env:TEMP with stamp $stamp"
```

Report both backup paths to the user before editing anything.

- [ ] **Step 5: Repoint Claude Code**

```powershell
$t = (Get-Content "$env:LOCALAPPDATA\McpGateway\token" -Raw).Trim()
claude mcp remove --scope user code-assist
claude mcp add --transport http --scope user code-assist http://127.0.0.1:7300/code-assist/mcp `
  --header "Authorization: Bearer $t" --header "X-Mcp-Client: code"
```

Verify:

```powershell
claude mcp get code-assist
```

Expected: transport `http`, the gateway URL, and both headers.

- [ ] **Step 6: Repoint Claude Desktop**

Edit `C:\Users\jorda\AppData\Roaming\Claude\claude_desktop_config.json`. Replace the `code-assist`
entry under `mcpServers` with, substituting the real token for `<TOKEN>`:

```json
    "code-assist": {
      "type": "http",
      "url": "http://127.0.0.1:7300/code-assist/mcp",
      "headers": {
        "Authorization": "Bearer <TOKEN>",
        "X-Mcp-Client": "desktop"
      }
    }
```

Confirm the file still parses:

```powershell
Get-Content C:\Users\jorda\AppData\Roaming\Claude\claude_desktop_config.json -Raw |
  ConvertFrom-Json | Select-Object -ExpandProperty mcpServers |
  Select-Object -ExpandProperty code-assist
```

- [ ] **Step 7: STOP — the user restarts their clients**

Ask the user to restart Claude Code and Claude Desktop, then call `mcp__code-assist__check_health`
in a new session and report what comes back.

Do not proceed until they confirm. Nothing below can be verified without a live client.

- [ ] **Step 8: Prove the lock is gone**

With sessions live and using CodeAssist, rebuild Release — the thing that used to require quitting
everything:

```powershell
dotnet build CodeAssistMcp\CodeAssistMcp.csproj -c Release -m:1 -v minimal
```

Expected: 0 errors. No `MSB3021`/`MSB3027` file-in-use failure. Nothing is running out of `bin/`
any more.

Then upgrade under live sessions:

```powershell
.\build\deploy.ps1 -Server code-assist
```

Expected: `Activated code-assist v-<old> -> v-<new>, 1 backend(s) swapped.` and no
`drainTimedOut` warning. Ask the user to call a CodeAssist tool immediately afterwards and confirm
it answers without an error.

- [ ] **Step 9: Confirm Desktop's negotiated protocol**

```powershell
Get-Content "$env:LOCALAPPDATA\McpServers\logs\code-assist\code-assist-*.log" -Tail 200 |
  Select-String -Pattern 'protocolVersion|Mcp-Session-Id|2026-07-28|2025-11-25'
```

Compare against Task 1's Stage 0 finding. If Desktop is on the handshake path, note in the spec that
its sessions reconnect on each upgrade — expected, not a defect.

- [ ] **Step 10: Record the outcome and commit**

Update the spec's `## Stage 0 findings` section with what Steps 8 and 9 showed, then:

```bash
git add servers.json docs/superpowers/specs/2026-08-30-mcp-http-gateway-design.md
git commit -m "chore: put code-assist behind the gateway"
```

Confirm no stray files: `git status --short` should be clean, and `$env:TEMP` should hold only the
two config backups, which stay until the user says the cutover is settled.
