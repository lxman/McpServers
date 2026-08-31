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
