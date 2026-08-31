#requires -Version 7
<#
.SYNOPSIS
Points the logon task at one published gateway version.

.DESCRIPTION
Registration no longer publishes. The gateway cannot be swapped in place the way a backend can --
one instance holds the port and the single-instance mutex -- so an upgrade is stop, repoint, start,
and the task action names the exact version directory it will run.

That makes the live version inspectable (Get-ScheduledTask McpGateway shows the path) and rollback
just this script with an older -Version.

deploy-gateway.ps1 runs the whole cycle; this is one step of it, and the one to reach for when
rolling back.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Version,
    [string] $TaskName = 'McpGateway'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$publishRoot = Join-Path $repoRoot "deploy\_gateway\$Version"
$assembly = Join-Path $publishRoot 'McpGateway.dll'

if (-not (Test-Path $assembly)) {
    throw "No gateway published at $publishRoot. Run publish-gateway.ps1 first."
}

$action = New-ScheduledTaskAction `
    -Execute 'dotnet' `
    -Argument "`"$assembly`"" `
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

Write-Host "'$TaskName' now points at $Version."
