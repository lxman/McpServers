#requires -Version 7
<#
.SYNOPSIS
Publish, repoint and restart the gateway, then prove the new build is the one running.

.DESCRIPTION
The gateway is the one component with no blue/green path: a second instance is refused by the
single-instance mutex and could not bind the port anyway. So the cycle is publish (while the old
one keeps serving), stop, repoint the task, start, verify.

Backends do not need to be stopped or cleaned up by hand. The job object kills them with the
gateway, and startup reconciliation clears anything it does not recognise -- this script leans on
that rather than reimplementing it.

Downtime is the stop-to-healthy window, a few seconds, plus whatever eagerStart backends take to
come back on their own afterwards.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $TaskName = 'McpGateway',
    [string] $GatewayUrl = 'http://127.0.0.1:7300',
    [int] $StopTimeoutSeconds = 30,
    [int] $HealthTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$port = ([uri]$GatewayUrl).Port

function Get-RegisteredVersion {
    param([string] $Name)

    try { $task = Get-ScheduledTask -TaskName $Name -ErrorAction Stop }
    catch { return $null }

    $argument = $task.Actions[0].Arguments
    if (-not $argument) { return $null }

    return Split-Path -Leaf (Split-Path -Parent $argument.Trim('"'))
}

function Test-PortBound {
    param([int] $Port)

    try { return @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop).Count -gt 0 }
    catch { return $false }
}

function Get-PortOwner {
    param([int] $Port)

    try {
        $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop |
            Select-Object -First 1
    }
    catch { return $null }

    if (-not $listener) { return $null }

    # Not $pid -- that is an automatic read-only variable in PowerShell.
    $owningPid = $listener.OwningProcess
    if (-not $owningPid) { return $null }

    try { return Get-CimInstance Win32_Process -Filter "ProcessId = $owningPid" -ErrorAction Stop }
    catch { return $null }
}

function Wait-PortFree {
    param([int] $Port, [int] $TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Test-PortBound -Port $Port) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }

    return -not (Test-PortBound -Port $Port)
}

$previous = Get-RegisteredVersion -Name $TaskName

# 1. Publish first, while the old gateway is still serving every client. This is the step that was
#    impossible before the deploy directory was versioned.
$version = & (Join-Path $PSScriptRoot 'publish-gateway.ps1') -Configuration $Configuration

if ($version -eq $previous) {
    throw "Published $version, which is already the registered version. Commit or wait a minute -- the version id is the short sha plus a minute-resolution stamp."
}

# 2. Stop, and wait for the port rather than for the task state. Task Scheduler reports NotRunning
#    as soon as it has asked the process to go, which is not the same as the socket being free, and
#    a new gateway that races the old one dies on the port instead of starting.
if ($previous) {
    Write-Host "Stopping $TaskName (running $previous)"

    # Stopping a task that is not running is not a failure here -- see the direct stop below.
    try { Stop-ScheduledTask -TaskName $TaskName -ErrorAction Stop }
    catch { Write-Host "  (task was not running)" }

    if (-not (Wait-PortFree -Port $port -TimeoutSeconds $StopTimeoutSeconds)) {
        # The gateway is not necessarily task-owned. It gets started by hand during a cutover and
        # while debugging, and Stop-ScheduledTask is then a silent no-op against a live process --
        # which is exactly how the first machine to run this script was set up. Fall back to
        # stopping the listener itself, but only once it has proved it is a gateway.
        $owner = Get-PortOwner -Port $port

        if (-not $owner) {
            throw "Port $port is still bound after ${StopTimeoutSeconds}s and its owner could not be identified. Nothing has been repointed; $previous is still registered."
        }

        if ($owner.CommandLine -notmatch 'McpGateway\.dll') {
            throw "Port $port is held by pid $($owner.ProcessId) ($($owner.Name)), which is not a gateway: $($owner.CommandLine). Nothing has been repointed; $previous is still registered."
        }

        Write-Host "  port still held by pid $($owner.ProcessId), which the task does not own; stopping it directly"

        # A hard stop is safe by construction: the job object kills the backends with the gateway,
        # and startup reconciliation clears anything it misses. That is what those exist for.
        Stop-Process -Id $owner.ProcessId -Force

        if (-not (Wait-PortFree -Port $port -TimeoutSeconds $StopTimeoutSeconds)) {
            throw "Port $port is still bound after stopping pid $($owner.ProcessId). Nothing has been repointed; $previous is still registered."
        }
    }
}

# 3. Repoint and start.
& (Join-Path $PSScriptRoot 'register-gateway-task.ps1') -Version $version -TaskName $TaskName

Write-Host "Starting $TaskName"
Start-ScheduledTask -TaskName $TaskName

# 4. Verify it answers. /admin/servers rather than /health: it is known to exist, it is behind the
#    same auth, and its body is what a deploy actually wants to see.
$tokenPath = Join-Path $env:LOCALAPPDATA 'McpGateway\token'
if (-not (Test-Path $tokenPath)) { throw "No gateway token at $tokenPath." }
$token = (Get-Content $tokenPath -Raw).Trim()

$deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
$servers = $null
while ($null -eq $servers -and (Get-Date) -lt $deadline) {
    try {
        $servers = Invoke-RestMethod -Method Get -Uri "$GatewayUrl/admin/servers" `
            -Headers @{ Authorization = "Bearer $token" }
    }
    catch { Start-Sleep -Milliseconds 500 }
}

if ($null -eq $servers) {
    throw "Gateway did not answer /admin/servers within ${HealthTimeoutSeconds}s. Roll back with: build\register-gateway-task.ps1 -Version $previous; Start-ScheduledTask -TaskName $TaskName"
}

# 5. Prove it is the NEW build answering. A gateway that never died, or a Task Scheduler restart of
#    the old action, would answer step 4 just as happily.
$expected = Join-Path $repoRoot "deploy\_gateway\$version\McpGateway.dll"
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.Contains($expected) })

if ($running.Count -eq 0) {
    throw "Something is answering on $port, but no process is running $expected. Check for a gateway started outside the task."
}

Write-Host ""
Write-Host "Gateway $previous -> $version (pid $($running[0].ProcessId))."

foreach ($name in $servers.PSObject.Properties.Name) {
    $entry = $servers.$name
    $backends = @($entry.backends)
    Write-Host "  $name active=$($entry.activeVersion) backends=$($backends.Count)"
}

Write-Host ""
Write-Host "Roll back with: build\register-gateway-task.ps1 -Version $previous; Restart-ScheduledTask -TaskName $TaskName"
