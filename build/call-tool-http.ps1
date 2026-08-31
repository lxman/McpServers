#requires -Version 7
<#
.SYNOPSIS
Calls one tool on an HTTP MCP endpoint and prints the raw JSON-RPC response.

.DESCRIPTION
The companion to dump-tools-http.ps1. That script proves a converted server's tool SURFACE matches
its stdio baseline; this one proves a tool actually RUNS over the new transport, which the schema
diff cannot show. Both are part of verifying a Stage 3 conversion.

.EXAMPLE
$token = (Get-Content "$env:LOCALAPPDATA\McpGateway\token" -Raw).Trim()
./call-tool-http.ps1 -Url http://127.0.0.1:7300/csharp-analyzer/mcp -Tool analyze_code `
    -Arguments @{ code = 'class C { }' } -Headers @{ Authorization = "Bearer $token" }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Url,
    [Parameter(Mandatory)][string] $Tool,
    [hashtable] $Arguments = @{},
    [hashtable] $Headers = @{}
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'McpSseResponse.ps1')

$Headers = InitializeMcpSession $Url $Headers

$callResponse = Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers `
    -ContentType 'application/json' `
    -Body (@{ jsonrpc = '2.0'; id = 2; method = 'tools/call'
              params = @{ name = $Tool; arguments = $Arguments } } | ConvertTo-Json -Depth 12)

ParseMcpBody $callResponse | ConvertTo-Json -Depth 14
