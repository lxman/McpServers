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
