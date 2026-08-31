# MCP HTTP Gateway — Design

Date: 2026-08-30
Status: approved for planning
Baseline commit: `146874c`

## Problem

Both client configs launch servers straight out of the build directory:

```
dotnet C:/Users/jorda/RiderProjects/McpServers/<Server>/bin/Release/net10.0/<Server>.dll
```

That path is the MSBuild output directory and the runtime directory at once. Every live server
process memory-maps the DLL, so Windows locks it and `dotnet build -c Release` fails. Clearing the
lock means quitting every client.

It's worse than one client's worth. Claude Code has 15 servers configured and Claude Desktop has 15
more, pointed at the same DLLs. At the time of the survey there were 20 live MCP server processes
holding about 1.4 GB, including 6 concurrent `SshMcp` and 2 `CodeAssistMcp`.

## Goals

1. Rebuild any server without stopping any client.
2. Upgrade a server without a running session seeing a failed tool call.
3. CodeAssist on HTTP as one shared instance rather than one process per session.
4. Servers on MCP protocol revision 2026-07-28.

## Non-goals

Redesigning any server's state model. That work is real and large (see Deferred work) but nothing
here depends on it.

Remote or LAN access. Everything binds `127.0.0.1`.

## What the investigation established

**Nothing blocks stateless mode.** All 14 servers and their 13 backing libraries were searched for
the features stateless HTTP disables — sampling, roots, elicitation, server-initiated notifications,
`IMcpServer` or `RequestContext` injection. Zero hits. No server registers resources or prompts
either; every one is tools-only.

**The SDK is ahead of the code.** `ModelContextProtocol` 2.2.0 is already referenced and supports
four revisions up to 2026-07-28, which removed the initialize handshake and `Mcp-Session-Id`
entirely. `SessionMode` defaults to `Stateless` at that revision, and `StatefulForInitializeClients`
serves legacy and modern clients on one endpoint. `ModelContextProtocol.AspNetCore` 2.2.0 is on
NuGet. Claude Code 2.1.251 speaks 2026-07-28 with legacy fallback.

**stdio has been supplying process-per-session isolation for free.** This is the real cost of the
project. A shared process breaks every server that has a "current" or "default" anything. Verified
directly:

- `RedisService` hardcodes a single `"default"` connection name for the whole process
  (`Libraries/RedisBrowser.Core/Services/RedisService.cs:14`). One session's `select_database(3)`
  plus another's `flush_database()` wipes db 3.
- `MongoConnectionManager._currentDatabases` is keyed by connection name only
  (`Libraries/Mcp.Database.Core/MongoDB/MongoConnectionManager.cs:19`), and no query tool takes a
  database parameter. `switch_database` silently redirects everyone.
- `TimeUtilities` stores timers in **process environment variables**
  (`McpUtilitiesServer/TimeUtilities.cs:144-199`). Two sessions timing "build" collide.
- `IndexStateStore.Delete` (`Libraries/CodeAssist.Core/Services/IndexStateStore.cs:220`) is the one
  method that doesn't take `_writeLock`.

Reported by survey, not independently verified: Azure credential selection, SQL transactions
visible across callers, `set_active_repository` clearing other repos' caches, Playwright and
Selenium `sessionId="default"` collisions.

## Architecture

One gateway process owns a stable port and supervises backends. Backends are pooled per server
according to a policy in the manifest.

```
                                    pool: shared      ┌──────────────┐
Claude Code ─┐                   ┌────────────────────│ code-assist  │
             ├─ :7300 gateway ───┤                    └──────────────┘
Claude Desktop ┘   + supervisor  │
                                 │  pool: per-client  ┌──────────────┐
                                 └────────────────────│ sql (code)   │
                                                      ├──────────────┤
                                                      │ sql (desktop)│
                                                      └──────────────┘
```

`shared` gives one backend to everyone. That's the point for CodeAssist — one graph, one hot cache,
one Qdrant client.

`per-client` gives each calling client its own backend, reproducing today's isolation. A server
converts to HTTP with no state redesign and no behavior change. This is what makes the bulk of the
migration mechanical.

Graduating a server from `per-client` to `shared` later is a manifest flag. The URL never changes,
so no config migration is ever needed again.

### Client identity

A static header per client: `X-Mcp-Client: code` on Claude Code, `desktop` on Claude Desktop.
Missing header means `default`. Deterministic, no inference.

This is per-*client*, not per-*session*. Two Claude Code windows would share a `per-client` backend
where today they don't. That's a real narrowing of isolation and it's listed under Risks.

**Stage 0 finding (2026-08-31): there is no per-session discriminator. The answer is no.**

Captured off the loopback adapter while a live Claude Code session called the running gateway, so
this is what the client actually sends, not what the docs say it sends:

```
Accept: application/json, text/event-stream
Accept-Encoding: identity
Authorization: Bearer <gateway token>
Content-Type: application/json
User-Agent: claude-code/2.1.251 (cli)
mcp-method: tools/call
mcp-name: check_health
mcp-protocol-version: 2026-07-28
x-mcp-client: code
Connection: keep-alive
Host: 127.0.0.1:7300
```

`x-mcp-client` is our own static header out of the client config. Nothing else varies per session,
per window, or per worktree. No `Mcp-Session-Id` in either direction, and that is not an accident:
the 2026-07-28 revision Claude Code negotiates removed it, so there is no protocol-level session
identity left to read. Three requests, identical header sets apart from `mcp-name`.

Two headers were unexpected and are worth keeping in mind: `mcp-method` and `mcp-name` mirror the
JSON-RPC method and the tool name into the request, so the gateway can route, log, or gate per tool
without parsing the body.

The only per-session key still on the table is the OS one. On loopback the gateway can map a
connection's remote port to its owning PID via `GetExtendedTcpTable`, and one Claude Code session is
one `claude` process.

**Adopted, 2026-08-31.** `pool: "per-session"` is now a third mode alongside `shared` and
`per-client`, keyed on `pid` plus process start time (pids get reused). It sits beside `per-client`
rather than replacing it, so `X-Mcp-Client` keeps its meaning if a second client application ever
returns, and every Stage 3 server opts in explicitly. A connection whose owner cannot be established
falls back to `default`, which is the behaviour the gateway had before per-session existed.

One configuration hazard comes with it: `per-session` together with `idleTimeoutMinutes: 0` never
reaps, so every session that ever connects leaves a backend behind. Nothing validates that yet.

### Gateway

New ASP.NET Core project, `McpGateway`. It has to stay boring — it's the single point of failure —
so it takes no dependency on any server and keeps its package list to ASP.NET Core, YARP, Serilog.

Routes:

| Route | Purpose |
|---|---|
| `POST /{server}/mcp` | forwarded to the backend for `(server, poolKey)` |
| `GET /{server}/health` | backend health, proxied |
| `GET /admin/servers` | status of every server and backend |
| `POST /admin/servers/{name}/activate` | blue/green swap to a version |
| `POST /admin/servers/{name}/restart` | force restart |
| `POST /admin/servers/{name}/stop` | stop backends |
| `POST /admin/prune` | delete inactive version directories with no live process |

Every route requires `Authorization: Bearer <token>`, compared in constant time. The token is 32
random bytes, base64url, generated on first run at `%LOCALAPPDATA%\McpGateway\token`.

Forwarding uses YARP's `IHttpForwarder` direct-forwarding API rather than static proxy config,
because the destination is chosen per request. YARP handles streaming response bodies correctly,
which matters — a streamable-HTTP POST response can be `text/event-stream`.

### Supervisor

Backends are keyed by `(serverName, poolKey)`, where `poolKey` is empty for `shared` and the client
id for `per-client`.

**Lazy start.** On the first request for a key, spawn
`dotnet <deployRoot>/<activeVersion>/<Server>.dll --mcp-port-file <temp>`. The backend binds
`127.0.0.1:0` and writes `{"port":…,"pid":…}` once Kestrel is up. The gateway watches for that file,
then polls `GET /health` until 200 or timeout. Concurrent requests for the same key await the same
task.

Requests during startup **wait**; they never get a 503 for being early. Lazy start costs first-call
latency, not errors. A backend that fails to write its port file, fails the health gate, or exits
during startup is a different case — those waiters get a 503 carrying the backend's last log lines,
and the failed start is not retried until the next request.

**Idle stop.** No request for `idleTimeout` (default 30 minutes, `0` disables) triggers
`POST /admin/shutdown` on the backend, then `Kill(entireProcessTree: true)` if it hasn't exited
within a grace period. This is what keeps 15 HTTP services from being a worse resource story than
today's stdio processes.

**Crash recovery.** An unexpected exit marks the backend dead. The next request lazily restarts it.

### Upgrade

`POST /admin/servers/{name}/activate {"version":"v-…"}`. For each live backend of that server:

1. Spawn the new version on a new port.
2. Health-gate it.
3. `Interlocked.Exchange` the route target.
4. Wait for in-flight requests against the old target to drain, with a timeout.
5. Shut the old one down.

Zero failed calls. If the new version fails to start or fails its health gate, the route is never
flipped, the new process is killed, and `activate` returns the failure with `activeVersion`
unchanged — the running backend is untouched. If drain times out, the old process is killed anyway;
requests still in flight at that point fail, so the drain timeout is the one window where an upgrade
can cost a call.

Some servers can't have two instances running at once — CodeAssist writes to a machine-wide
`%LocalAppData%\CodeAssist\indexes`. Those get `overlapAllowed: false` in the manifest and take a
stop-then-start path instead: drain, stop, start the new version, health-gate, release the held
requests. Because the gateway holds requests rather than refusing them, that path also produces zero
failed calls; it costs latency instead — bounded by the server's `startupTimeoutSeconds`, which for
CodeAssist is the graph build, not milliseconds.

If the new version fails its health gate on that path there is no old process to fall back to, so
the gateway restarts the previous version and reports the failure. Held requests survive if that
restart succeeds.

Rollback is `activate` with the previous version id.

### Hosting library

New library, `Mcp.Hosting.Core`. Each server's `Program.cs` changes shape but not content:

```csharp
var builder = McpHttpHost.CreateBuilder(args, "code-assist");

builder.Services.AddCodeAssistServices(builder.Configuration);

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
    .WithTools<HealthTools>()
    // ...unchanged
    ;

var app = builder.Build();
app.MapMcpHost();
await app.RunAsync();
```

`CreateBuilder` sets content root to `AppContext.BaseDirectory`, binds Kestrel to `127.0.0.1:0`,
reads `--mcp-port-file`, and points Serilog at an **absolute** per-server directory:
`%LOCALAPPDATA%\McpServers\logs\<name>\<name>-.log`. That replaces the current CWD-relative
`logs/…` paths, which today land wherever the client happened to be started.

`MapMcpHost()` maps `/mcp`, `/health` and `/admin/shutdown`, guards all three with a bearer token
compared in constant time, and writes the port file on `ApplicationStarted`. The token arrives in
`MCP_SHUTDOWN_TOKEN` -- a name that predates the widening from shutdown-only to every endpoint --
and a backend started without one refuses to start rather than serving unauthenticated. It is a
*different* token from the client-facing one: the gateway mints it per run, keeps it in memory
and never writes it to `%LOCALAPPDATA%\McpGateway	oken`, so a client holding the gateway token
still cannot reach a backend port directly.

The library also exposes `McpCaller.ClientId`, reading `X-Mcp-Client`. Unused in stage 3; it's what
stage 4 needs to scope state per caller.

### Deploy layout

```
deploy/
  code-assist/
    v-146874c-20260830T1214/
    v-3fae868-20260831T0902/
  sql/
    ...
```

Version id is `v-<short-sha>-<utc-timestamp>`. The active version is recorded in a runtime state
file, `%LOCALAPPDATA%\McpGateway\state.json` — no junctions, so no Windows junction-retarget or
lock edge cases, and rollback is one field. It is deliberately *not* in `servers.json`: that file is
git-tracked, so writing to it on every activation would dirty the working tree, and a `checkout`,
`stash` or `pull` would silently revert a live server's version.

Publish with `dotnet publish <proj> -c Release -o deploy/<name>/<version>`. Nothing runs out of
`bin/` again, which is what actually removes the lock.

### Manifest

Static config in `servers.json`, git-tracked and never written by the gateway:

```json
{
  "code-assist": {
    "project": "CodeAssistMcp/CodeAssistMcp.csproj",
    "assembly": "CodeAssistMcp.dll",
    "deployRoot": "deploy/code-assist",
    "pool": "shared",
    "overlapAllowed": false,
    "eagerStart": true,
    "idleTimeoutMinutes": 0,
    "startupTimeoutSeconds": 60
  }
}
```

Runtime state in `%LOCALAPPDATA%\McpGateway\state.json`, written by the gateway and by nothing
else:

```json
{
  "activeVersions": {
    "code-assist": "v-146874c-20260830T1214"
  }
}
```

`ManifestStore` reads both and merges. A server with no recorded version has never been deployed:
starting it fails with a 503 saying so, rather than resolving to a deploy directory named after a
placeholder.

### Client config

```
claude mcp add --transport http --scope user code-assist \
  http://127.0.0.1:7300/code-assist/mcp \
  -H "Authorization: Bearer <token>" -H "X-Mcp-Client: code"
```

Claude Desktop gets the same URL with `X-Mcp-Client: desktop`.

### Startup

Task Scheduler at logon, running as the user. Not a Windows Service: several servers need the
interactive user session and user-profile credentials — browser automation, desktop automation, SSH
keys, AWS and Azure credential stores.

### Protocol

`SessionMode = StatefulForInitializeClients`. Claude Code is served statelessly on 2026-07-28;
anything older gets a handshake session on the same endpoint.

One interaction to note: a blue/green route flip breaks a *stateful* session, because the new
process doesn't have it. Legacy-path clients will see a reconnect on upgrade. That's a reason to
pin down Claude Desktop's negotiated revision early.

## Testing

Gateway unit tests cover pool keying, lazy start, the health gate, drain, activate, rollback, and
auth rejection.

Integration tests use a trivial `TestMcpBackend` fixture: start the gateway, drive tool calls in a
loop, activate a new version mid-flight, assert zero failures and that responses switch versions.

The regression gate for the 13 mechanical conversions is a tool-schema diff. Dump `tools/list` from
the stdio build and the HTTP build and compare. A conversion that changes the tool surface fails.

Per the standing rule about tests that pass whether or not the fix exists: every gateway test gets
checked by breaking the thing it tests and confirming it goes red.

## Sequencing

**Stage 0 — spike. Done 2026-08-31, and no listener was needed.** The three questions: does Claude
Code send anything that distinguishes a session rather than a client; what revision does Claude
Desktop negotiate; what request timeout does the client apply, so lazy start can stay inside it.

Answered by capturing the loopback traffic of a live session against the running gateway rather than
building the throwaway listener. The gateway was already serving a real client, so the probe would
only have reproduced what was already on the wire.

1. **Session discriminator: no.** See Client identity. Per-client pooling cannot be promoted to
   per-session by reading a header.
2. **Claude Desktop's revision: moot.** Desktop's `mcpServers` is empty and the user does not use
   it, so there is exactly one client. That is why question 1 matters more now than when this spec
   was written.
3. **Client request timeout: not measured.** Nothing in the capture reveals it. code-assist runs
   `eagerStart: true` with `startupTimeoutSeconds: 120` and has served a live session without a
   client-side timeout, which is evidence enough for the one server converted so far. Still open for
   the slower Stage 3 servers.

**Stage 1 — infrastructure.** `Mcp.Hosting.Core`, `McpGateway`, deploy layout, publish and activate
scripts, Task Scheduler registration. Validated against `TestMcpBackend`, no real server touched.

**Stage 2 — CodeAssist.** Convert to HTTP, `pool: shared`, `overlapAllowed: false`,
`eagerStart: true`. Fix the `IndexStateStore.Delete` lock gap and `set_active_repository`'s default
cache clearing, both of which become cross-session hazards under sharing. Delivers goal 3.

**Stage 3 — the other 13.** Convert as `per-client`. Mechanical, gated by the tool-schema diff.
Delivers goals 1, 2 and 4 for everything. Order by risk: `time-utility`, `csharp-analyzer`, `edgar`,
`document`, then `aws`, `azure`, then `sql`, `mongo`, `redis`, `ssh`, `desktop-commander`,
`selenium`, `playwright`.

**Stage 4 — deferred.** Graduate servers from `per-client` to `shared` as their state models get
fixed. Each server is its own small piece of work. Nothing above depends on it.

## Risks

**Gateway is a single point of failure.** If it's down, every server is unreachable. Mitigated by
keeping it small, giving Task Scheduler restart-on-failure, and holding no state that can't be
rebuilt from `servers.json`.

**Localhost HTTP is reachable by every local process.** Today stdio means only the parent client can
reach `desktop-commander` or `ssh-mcp`. As services, any local process could. The bearer token is
load-bearing, not decoration.

**Per-client is not per-session. Confirmed by Stage 0, and worse than written.** Two Claude Code
windows share a `per-client` backend. Stage 0 did not close this; it established that no header can
close it. And with Claude Desktop retired there is exactly one client, so on a `per-client` server
`X-Mcp-Client` takes exactly one value — `per-client` and `shared` are now the same pool.

Stage 3 converts 13 servers on the assumption that `per-client` preserves isolation. It does not.
The ambient-state hazards listed above — `switch_database`, the single Redis connection, cross-caller
SQL transactions, ssh connection state, desktop-commander sessions — go live for every concurrent
Claude Code session the moment those servers convert. Settle this before Stage 3 starts, not during.

**Scoped-service lifetime may differ between hosts.** Under the web host a DI scope is per HTTP
request; the generic host's scoping under the stdio transport may not match. `SeleniumMcp` registers
scoped services holding a live ChromeDriver, so it's the sharpest case. Verify per server; Selenium
first.

**Lazy-start latency could exceed a client timeout** on the slower servers. `eagerStart: true` is
the escape hatch, set for CodeAssist from the start.

## Out of scope — separate tickets

- `netdbg` is dead config. `DebugMcp`'s source was removed in `a12ebd6`; only `bin/` survives, and
  both client configs still launch it.
- `CSharpAnalyzerMcp` — `AssemblyAnalysisService` takes `AssemblyLoaderService`
  (`Libraries/CSharpAnalyzer.Core/Services/Reflection/AssemblyAnalysisService.cs:9`) which is never
  registered; `CSharpAnalyzerMcp/Program.cs:22` is the only registration. Tools list, then throw on
  call.
- `SqlMcp` — `SqlConnectionTools` takes `IConnectionManager`
  (`SqlMcp/Tools/SqlConnectionTools.cs:12`), implemented only in `SqlServer.Core`, but
  `AddSqlConnectionManager()` registers the concrete `SqlConnectionManager` from
  `Mcp.Database.Core` instead. Never registered, so the tool class can't be constructed.
- SSH connection passwords and passphrases are stored in plaintext at
  `%APPDATA%\SshMcp\profiles.json`, and `connect_with_profile` authenticates by profile name.
- `DocumentMcp`'s `PasswordManager` is a single global store with no owner scoping.
