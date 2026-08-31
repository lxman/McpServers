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

# The apphost, not `dotnet McpGateway.dll`. dotnet.exe is a console-subsystem binary, so launching
# through it produces a console window no matter what the app is built as -- and closing that
# window kills the gateway outright (CTRL_CLOSE_EVENT -> 0xC000013A). McpGateway is built WinExe,
# so running its apphost directly allocates no console at all.
$executable = Join-Path $publishRoot 'McpGateway.exe'

if (-not (Test-Path $executable)) {
    throw "No gateway published at $publishRoot. Run publish-gateway.ps1 first."
}

$action = New-ScheduledTaskAction `
    -Execute $executable `
    -WorkingDirectory $repoRoot

$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME

# A watchdog, not a schedule. RestartOnFailure below only fires when a task FAILS, and a task that
# was STOPPED did not fail -- so a gateway that goes away for any reason leaves the task sitting in
# Ready and nothing ever brings it back. That has been observed repeatedly: exit 0xC000013A
# (STATUS_CONTROL_C_EXIT) with nothing logged, cause still unidentified.
#
# Repeating the logon trigger indefinitely closes that hole without needing to know the cause.
# MultipleInstancesPolicy is IgnoreNew, so a repetition that lands while the gateway is healthy is
# discarded; one that lands while it is dead starts it. Worst case the gateway is down for the
# repetition interval rather than until someone notices.
$trigger.Repetition = (New-ScheduledTaskTrigger `
    -Once -At (Get-Date) `
    -RepetitionInterval (New-TimeSpan -Minutes 5)).Repetition

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero)

# New-ScheduledTaskSettingsSet fills IdleSettings in with StopOnIdleEnd = true, and there is no
# parameter to turn it off -- it has to be assigned afterwards. Left at the default it means "stop
# the task when the computer stops being idle", and Task Scheduler stops a task by terminating it:
# the gateway exits 0xC000013A (STATUS_CONTROL_C_EXIT) having logged nothing, taking every backend
# with it via the job object. It does not come back, either, because a task that was STOPPED did
# not FAIL, so RestartOnFailure never fires and the task simply sits in Ready.
#
# This cost two silent gateway deaths to find. The hand-started gateway it replaced ran for six
# hours precisely because Task Scheduler did not own it.
$settings.IdleSettings.StopOnIdleEnd = $false
$settings.IdleSettings.RestartOnIdle = $false

# Belt and braces: idle settings are documented as applying mainly when RunOnlyIfIdle is set, and
# it defaults to false -- but the observed behaviour says otherwise, so state both explicitly
# rather than relying on which reading is right.
$settings.RunOnlyIfIdle = $false

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -Force -RunLevel Limited | Out-Null

Write-Host "'$TaskName' now points at $Version."
