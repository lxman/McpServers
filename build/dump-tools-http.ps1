#requires -Version 7
<#
.SYNOPSIS
Drives an HTTP MCP endpoint through initialize + tools/list and prints its tool surface, sorted.

.DESCRIPTION
Uses the 2025-11-25 handshake, which StatefulForInitializeClients still serves, so the output is
directly comparable with the stdio baseline.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Url,
    [hashtable] $Headers = @{}
)

$ErrorActionPreference = 'Stop'

# The pinned SDK returns 406 unless both media types are accepted, but once text/event-stream is
# accepted the server answers as SSE ("event: message\ndata: {...}") rather than plain JSON -- so
# the JSON-RPC payload has to be pulled out of the "data:" line rather than parsed as a whole body.
$Headers['Accept'] = 'application/json, text/event-stream'

function ParseMcpBody($response) {
    $contentType = [string]$response.Headers['Content-Type']
    if ($contentType -like 'text/event-stream*') {
        $dataLine = ($response.Content -split "`n") | Where-Object { $_ -like 'data:*' } | Select-Object -First 1
        if (-not $dataLine) { throw "No 'data:' line found in SSE response: $($response.Content)" }
        return ($dataLine -replace '^data:\s*', '') | ConvertFrom-Json
    }
    return $response.Content | ConvertFrom-Json
}

$init = @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{
    protocolVersion = '2025-11-25'; capabilities = @{}
    clientInfo = @{ name = 'tool-parity'; version = '1' } } } | ConvertTo-Json -Depth 12

$response = Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers `
    -ContentType 'application/json' -Body $init

$sessionId = $response.Headers['Mcp-Session-Id']
if ($sessionId) { $Headers['Mcp-Session-Id'] = [string]$sessionId }

Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers -ContentType 'application/json' `
    -Body (@{ jsonrpc = '2.0'; method = 'notifications/initialized' } | ConvertTo-Json) | Out-Null

$listResponse = Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers `
    -ContentType 'application/json' `
    -Body (@{ jsonrpc = '2.0'; id = 2; method = 'tools/list' } | ConvertTo-Json)
$list = ParseMcpBody $listResponse

$list.result.tools |
    Sort-Object name |
    Select-Object name, description, inputSchema |
    ConvertTo-Json -Depth 20
