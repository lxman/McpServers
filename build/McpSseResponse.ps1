#requires -Version 7
<#
.SYNOPSIS
Parses an MCP HTTP response body, which is either plain JSON or a Server-Sent Events stream.

.DESCRIPTION
Dot-source this from any script that talks to an MCP HTTP endpoint. It was extracted from
dump-tools-http.ps1 when a second caller appeared; the parsing is subtle enough that a second copy
would have drifted from the first.

What it handles (per the SSE spec -- https://html.spec.whatwg.org/multipage/server-sent-events.html):
  - Plain application/json bodies (parsed directly, no SSE involved).
  - An SSE body containing more than one event, separated by a blank line -- e.g. a keep-alive
    or "ping" event delivered ahead of the real "message" event. Every event's assembled data is
    parsed and the one that actually decodes to a JSON-RPC payload (has a "jsonrpc" property) is
    selected, preferring an event literally named "message" but not requiring it, rather than
    assuming the JSON-RPC response is the first event or the first "data:" line.
  - A single event's data split across multiple contiguous "data:" lines (e.g. a pretty-printed
    JSON body) -- per spec these are joined with LF into one payload before parsing, not just the
    first line taken.

What it does NOT handle: a single event streamed across multiple separate HTTP responses/chunks,
SSE comment lines (lines starting with ":") carrying meaningful data, or "id:"/"retry:" fields
(fine for a one-shot request/response dump; a script that needs to resume a stream would need
more). A future adaptation for another server should check whether any of those apply there.

Fails loudly (throws) if no event yields a recognisable JSON-RPC payload, rather than returning
null/empty -- that silent-empty failure mode is exactly what made the original bug this replaces
expensive to find: Invoke-RestMethod returned nothing and raised no error.
#>

function ParseMcpBody($response) {
    $contentType = [string]$response.Headers['Content-Type']

    if ($contentType -notlike 'text/event-stream*') {
        return $response.Content | ConvertFrom-Json
    }

    $normalized = $response.Content -replace "`r`n", "`n" -replace "`r", "`n"
    $eventBlocks = $normalized -split "`n`n" | Where-Object { $_.Trim().Length -gt 0 }
    if ($eventBlocks.Count -eq 0) {
        throw "SSE response contained no events: $($response.Content)"
    }

    $candidates = foreach ($block in $eventBlocks) {
        $dataLines = @()
        $eventName = $null
        foreach ($line in ($block -split "`n")) {
            if ($line -like 'data:*') {
                $dataLines += ($line -replace '^data:\s?', '')
            } elseif ($line -like 'event:*') {
                $eventName = ($line -replace '^event:\s?', '').Trim()
            }
        }
        if ($dataLines.Count -eq 0) { continue }

        # Contiguous data: lines belonging to one event are joined with LF into a single payload,
        # per spec -- not just the first line of the event.
        $payloadText = $dataLines -join "`n"
        try {
            $payload = $payloadText | ConvertFrom-Json -ErrorAction Stop
        } catch {
            continue
        }

        [pscustomobject]@{ EventName = $eventName; Payload = $payload }
    }

    $jsonRpc = $candidates |
        Where-Object { $_.EventName -eq 'message' -and $null -ne $_.Payload.jsonrpc } |
        Select-Object -First 1
    if (-not $jsonRpc) {
        # Fall back to any event whose data looks like JSON-RPC, in case the event name differs.
        $jsonRpc = $candidates | Where-Object { $null -ne $_.Payload.jsonrpc } | Select-Object -First 1
    }

    if (-not $jsonRpc) {
        throw "No event in the SSE response carried a recognisable JSON-RPC payload: $($response.Content)"
    }

    return $jsonRpc.Payload
}

<#
.SYNOPSIS
Runs the MCP initialize handshake against $Url and returns the headers to use for later calls.

.DESCRIPTION
Uses the 2025-11-25 handshake, which StatefulForInitializeClients still serves. That is deliberate:
a 2026-07-28 client must also send Mcp-Method and Mcp-Name headers and _meta protocolVersion and
clientCapabilities in the body, or tools/call is rejected. Nothing here needs the newer protocol.
#>
function InitializeMcpSession([string] $Url, [hashtable] $Headers) {
    $Headers['Accept'] = 'application/json, text/event-stream'

    $init = @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{
        protocolVersion = '2025-11-25'; capabilities = @{}
        clientInfo = @{ name = 'tool-parity'; version = '1' } } } | ConvertTo-Json -Depth 12

    $response = Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers `
        -ContentType 'application/json' -Body $init

    $sessionId = $response.Headers['Mcp-Session-Id']
    if ($sessionId) { $Headers['Mcp-Session-Id'] = [string]$sessionId }

    Invoke-WebRequest -Method Post -Uri $Url -Headers $Headers -ContentType 'application/json' `
        -Body (@{ jsonrpc = '2.0'; method = 'notifications/initialized' } | ConvertTo-Json) | Out-Null

    return $Headers
}
