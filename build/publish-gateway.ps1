#requires -Version 7
<#
.SYNOPSIS
Publishes the gateway to a fresh versioned deploy directory and prints the version id.

.DESCRIPTION
The gateway was the last component still published into a fixed directory, which made it the one
thing that could not be rebuilt while it was running: dotnet publish cannot write over a DLL the
live process holds open. Each publish now gets its own directory, exactly as publish.ps1 does for
the servers, so a build never contends with the running gateway.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$sha = (git -C $repoRoot rev-parse --short HEAD).Trim()
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmm')
$version = "v-$sha-$stamp"

$output = Join-Path $repoRoot "deploy\_gateway\$version"
$project = Join-Path $repoRoot 'McpGateway\McpGateway.csproj'

# Same reason as publish.ps1: dotnet publish BUILDS into bin/ and obj/ before copying to -o, and
# those are precisely the paths the running gateway holds locked. Redirecting the whole build tree
# keeps the publish off them.
$artifacts = Join-Path $env:TEMP 'mcp-publish-artifacts\_gateway'

Write-Host "Publishing gateway -> $output"
dotnet publish $project -c $Configuration -o $output --artifacts-path $artifacts --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Gateway publish failed.' }

# The apphost is what the task runs, so it is what must exist -- checking only the dll would let a
# publish that somehow skipped the apphost register a task that cannot start.
$executable = Join-Path $output 'McpGateway.exe'
if (-not (Test-Path $executable)) { throw "Publish produced no McpGateway.exe at $output." }

Write-Output $version
