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

# --artifacts-path is load-bearing, not tidiness. `dotnet publish` BUILDS into bin/<config> and
# obj/ before copying to -o, and those are exactly the paths a running stdio server holds locked --
# so the very first publish would fail on the lock this project exists to remove. Redirecting the
# whole build tree (the referenced libraries too, which are equally locked) keeps bin/ untouched.
$artifacts = Join-Path $env:TEMP "mcp-publish-artifacts\$Server"

Write-Host "Publishing $Server -> $output"
dotnet publish $project -c $Configuration -o $output --artifacts-path $artifacts --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Server." }

$assembly = Join-Path $output $entry.assembly
if (-not (Test-Path $assembly)) { throw "Publish produced no $($entry.assembly) at $output." }

Write-Output $version
