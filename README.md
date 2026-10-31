# McpServers

This project is a personally curated set of tools based on my last six months of Claude. I've found it to be a valuable tool and these tools are designed to extend his usefulness to enterprise developers.

## Why did I choose this particular design setup?

The particular setup was based on a few design decisions:

1. **Why C#?** - Because I'm a .NET kinda guy. I'm iconoclastic like that.
2. **Why these servers instead any of the thousands of others in existence?** - Good question. I've tried some of the others and I wasn't happy with some of the limitations or deficiencies that they had, some by design and some not. I have to explain that these servers don't have the guard rails that I have seen in others. There are directory restrictions but Claude can overwrite those if he feels the need. Some of the most dangerous commands are blacklisted (`rm -rf`, `format c` and stuff like that - Claude cannot bypass these) but otherwise I consider that my relationship with Claude has worked fairly smoothly. If I don't want him to start modifying code on disk I just tell him not to and 99% of the time he respects that. It's always best to have a clean git commit in place before you start working an idea with Claude anyway just in case he does decide to go off the rails. So, in short, these guard rails work for me. YMMV
2. **Why this particular server arrangement?** - In designing these tools I originally set them all up as separate STDIO MCP servers. Those are convenient because Claude initializes them when it starts up and automatically receives a description of the tools, what they do, how to use them, etc. That sounds convenient, and it is, but there's a cost there that I discovered. Everyone using an AI these days for development has one overarching concern while doing so, token consumption. You're always fighting to either stay under your limit or minimize how many you use. I have the top of the line Anthropic subscription for my personal use and with Sonnet 4.5 I am allowed 190k tokens per conversation. My conversations were being cut off sooner and sooner and after discussing it with Claude I finally discovered why. Reading the initial instructions and descriptions of all of these MCP servers was costing about 100k tokens. Over half of my budget was burned up with Claude just getting to know how to use the tools I was providing him. So the architecture that I finally decided on is based on a fairly new concept in the AI world. Anthropic's version of it is called Agent Skills. It's not really a new-fangled thing. It's just a different viewpoint. Instead of tagging all of the tools and their parameters you simply add a very brief description with a "for more information see . . ." and then construct a hierarchy of `md` files with the bulk of the information, examples, etc. That way relatively few tokens are spent reading the original descriptions and we only spend the cost per tool as Claude decides that it needs to use them. This defers the expense nicely.
3. **Why Go, Python and Typescript?** - Those particular tools are for the analysis of those particular kinds of code. It's a bit janky to have C# tools try to analyze another language's code. While it can be done I find the path much more straightforward to use Go to analyze Go, Python to analyze Python, etc. Fortunately with Claude working with other languages is much simpler.

I think that explains the main reasons for what some may consider an unusual design philosophy.

## Running it

The servers no longer run as one stdio process per client. A single gateway process (`McpGateway`) owns `http://127.0.0.1:7300`, starts each server on demand, and proxies MCP requests to it. Running servers load their assemblies from versioned directories under `deploy/`, never from `bin/`, so a rebuild never fights a file lock and a deploy never requires stopping clients.

### Prerequisites

- .NET 10 SDK
- PowerShell 7 (`pwsh`)
- Windows (Task Scheduler registration and job-object supervision are Windows-specific)

### Build

```powershell
dotnet build McpServers.slnx -c Release
```

This builds everything into `bin/`, which is only a build tree now. Nothing runs from it.

### Publish and activate a server

Each publish gets its own directory, `deploy/<server>/v-<sha>-<timestamp>/`. The gateway records which version is active in `%LOCALAPPDATA%\McpGateway\state.json`.

```powershell
# 1. Publish a new version (safe while the old one is serving)
$version = pwsh build\publish.ps1 -Server desktop-commander

# 2. Ask the gateway to swap to it
pwsh build\activate.ps1 -Server desktop-commander -Version $version
```

`activate` starts the new version, health-gates it, flips the route, drains in-flight requests, and stops the old one. Servers that cannot run two instances at once (`overlapAllowed: false` in `servers.json`, e.g. code-assist) take a stop-then-start path that holds requests instead of failing them.

### Upgrade the gateway

The gateway is the one component without a blue/green path — a second instance is refused by its single-instance mutex and could not bind the port anyway. The cycle is publish (while the old gateway keeps serving), stop, repoint the scheduled task, start, verify.

```powershell
pwsh build\deploy-gateway.ps1
```

To roll back, repoint the task at an older version and restart it:

```powershell
pwsh build\register-gateway-task.ps1 -Version v-<older-version>
Stop-ScheduledTask -TaskName McpGateway
Start-ScheduledTask -TaskName McpGateway
```

### Start it automatically

`build\register-gateway-task.ps1` registers a logon task named `McpGateway` that runs as the interactive user (several servers need user-profile credentials and the interactive session). It includes a 5-minute repetition watchdog and disables idle-stop, both of which exist because Task Scheduler otherwise terminates the gateway silently.

### Point a client at it

Every server is a streamable-HTTP endpoint at `http://127.0.0.1:7300/<server>/mcp`. Requests need the gateway token from `%LOCALAPPDATA%\McpGateway\token` and a static client header:

```powershell
claude mcp add --transport http --scope user desktop-commander `
  http://127.0.0.1:7300/desktop-commander/mcp `
  -H "Authorization: Bearer <token>" -H "X-Mcp-Client: code"
```

In OpenCode (`~/.config/opencode/opencode.json`):

```json
"desktop-commander": {
  "type": "remote",
  "url": "http://127.0.0.1:7300/desktop-commander/mcp",
  "oauth": false,
  "headers": {
    "Authorization": "Bearer <token>",
    "X-Mcp-Client": "code"
  }
}
```

Because the URL never changes, a redeploy needs no client-config migration.

### Admin endpoints

All routes require `Authorization: Bearer <token>`.

| Route | Purpose |
|---|---|
| `GET /admin/servers` | status of every server and its live backends |
| `POST /admin/servers/{name}/activate` | blue/green swap to `{"version":"v-…"}` |
| `POST /admin/servers/{name}/restart` | force restart |
| `POST /admin/servers/{name}/stop` | stop backends |
| `POST /admin/prune` | delete superseded version directories with no live process |

### Logs, data, and versions

- Logs: `%LOCALAPPDATA%\McpServers\logs\<server>\<server>-.log`
- Data: `%LOCALAPPDATA%\McpServers\data\<server>\`
- Runtime state: `%LOCALAPPDATA%\McpGateway\state.json` (git-tracked config lives in `servers.json` and is never written by the gateway)

Old versions stay in `deploy/` until pruned, which is what makes rollback one `activate` call.

### Non-.NET analyzers

The Go, Python and Typescript analyzers are separate projects, not gateway-managed .NET servers. Each folder has a `run.bat` to help set it up.

Cross-platform? I'm not actually sure. It seems I recall throwing a few Windows-specific bits in there so you would have to try it and see.

And of course, this is an ongoing project, so there is no guarantee on breaking changes.