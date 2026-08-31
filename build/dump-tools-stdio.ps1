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
