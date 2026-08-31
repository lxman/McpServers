#requires -Version 7
<#
.SYNOPSIS
Drives an HTTP MCP endpoint through initialize + tools/list and prints its tool surface, sorted.

.DESCRIPTION
Uses the 2025-11-25 handshake, which StatefulForInitializeClients still serves, so the output is
directly comparable with the stdio baseline.

The SSE parsing and the handshake live in McpSseResponse.ps1, shared with call-tool-http.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Url,
    [hashtable] $Headers = @{}
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'McpSseResponse.ps1')

# The pinned SDK returns 406 unless both media types are accepted, but once text/event-stream is
# accepted the server answers as SSE ("event: message\ndata: {...}") rather than plain JSON -- so
# the JSON-RPC payload has to be pulled out of the SSE body rather than parsed as a whole body.
$Headers = InitializeMcpSession $Url $Headers

$listResponse = Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers `
    -ContentType 'application/json' `
    -Body (@{ jsonrpc = '2.0'; id = 2; method = 'tools/list' } | ConvertTo-Json)
$list = ParseMcpBody $listResponse

$list.result.tools |
    Sort-Object name |
    Select-Object name, description, inputSchema |
    ConvertTo-Json -Depth 20
